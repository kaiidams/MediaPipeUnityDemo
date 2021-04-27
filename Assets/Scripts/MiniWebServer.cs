using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public class MiniWebServer : MonoBehaviour
{
    enum HTTPState
    {
        WaitingForHeader, WebSocket
    }

    private readonly byte[] HeaderEnd = new byte[] { 13, 10, 13, 10 };

    public int listenPort = 5555;
    public MediaPipeIKControl ikControl;

    Thread thread;
    bool isCanceled;
    private LandmarkDemo demo;
    private ConcurrentQueue<string> queue;
    private Dictionary<string, byte[]> contents;

    // Start is called before the first frame update
    void Start()
    {
        this.isCanceled = false;
        this.demo = GetComponent<LandmarkDemo>();
        this.contents = new Dictionary<string, byte[]>();
        this.contents.Add("index.html", Resources.Load<TextAsset>("index_html").bytes);
        this.contents.Add("index.js", Resources.Load<TextAsset>("index_js").bytes);
        this.queue = new ConcurrentQueue<string>();
        this.thread = new Thread(Run);
        this.thread.Start();
    }

    // Update is called once per frame
    void Update()
    {
        // Fetch query and handle
        string message;
        if (queue.TryDequeue(out message))
        {
            this.demo.OnPoseResults(message);
            this.ikControl.OnPoseResults(message);
        }
    }

    public void OnDestroy()
    {
        this.isCanceled = true;
    }

    public void Run()
    {
        ListenAsync().Wait();
    }

    public async Task ListenAsync()
    {
        var server = new TcpListener(IPAddress.Parse("0.0.0.0"), this.listenPort);
        server.Start();
        print("Server has started on 127.0.0.1:80.{0}Waiting for a connection...");
        while (!isCanceled)
        {
            var client = await server.AcceptTcpClientAsync();
            ServeClient(client);
        }
    }

    private async void ServeClient(TcpClient client)
    {
        try
        {
            using (client)
            {
                using (var stream = client.GetStream())
                {
                    await ProcessAsync(stream);
                }
            }
        }
        catch (Exception ex)
        {
            print(ex);
        }
    }

    public async Task ProcessAsync(Stream stream)
    {
        var bytes = new byte[8 * 1024];
        var searchIndex = 0;
        var headerBuffer = new MemoryStream();
        HTTPState state = HTTPState.WaitingForHeader;

        //enter to an infinite cycle to be able to handle every change in stream
        while (true)
        {
            int read = await stream.ReadAsync(bytes, 0, bytes.Length);
            if (read == 0)
            {
                break;
            }
            int pos = 0;
            while (pos < read)
            {
                if (state == HTTPState.WaitingForHeader)
                {
                    pos = FindHeaderEnd(bytes, pos, read, ref searchIndex);
                    if (pos == -1)
                    {
                        pos = read;
                        await headerBuffer.WriteAsync(bytes, 0, pos);
                        continue;
                    }
                    await headerBuffer.WriteAsync(bytes, 0, pos);
                    if (searchIndex >= HeaderEnd.Length)
                    {
                        Console.WriteLine("Header received");
                        string headers = Encoding.UTF8.GetString(headerBuffer.GetBuffer());

                        if (Regex.IsMatch(headers, "^GET") && Regex.IsMatch(headers, "Connection: Upgrade"))
                        {
                            print($"=====Handshaking from client=====\n{headers}");

                            // 1. Obtain the value of the "Sec-WebSocket-Key" request header without any leading or trailing whitespace
                            // 2. Concatenate it with "258EAFA5-E914-47DA-95CA-C5AB0DC85B11" (a special GUID specified by RFC 6455)
                            // 3. Compute SHA-1 and Base64 hash of the new value
                            // 4. Write the hash back as the value of "Sec-WebSocket-Accept" response header in an HTTP response
                            string swk = Regex.Match(headers, "Sec-WebSocket-Key: (.*)").Groups[1].Value.Trim();
                            string swka = swk + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
                            byte[] swkaSha1 = System.Security.Cryptography.SHA1.Create().ComputeHash(Encoding.UTF8.GetBytes(swka));
                            string swkaSha1Base64 = Convert.ToBase64String(swkaSha1);

                            // HTTP/1.1 defines the sequence CR LF as the end-of-line marker
                            byte[] response = Encoding.UTF8.GetBytes(
                                "HTTP/1.1 101 Switching Protocols\r\n" +
                                "Connection: Upgrade\r\n" +
                                "Upgrade: websocket\r\n" +
                                "Sec-WebSocket-Accept: " + swkaSha1Base64 + "\r\n\r\n");

                            stream.Write(response, 0, response.Length);

                            state = HTTPState.WebSocket;
                        }
                        else
                        {
                            var url = Regex.Match(headers, "^GET ([^ ]+)").Groups[1].Value.Trim();
                            print(url);
                            byte[] response = Encoding.UTF8.GetBytes(
                                "HTTP/1.1 200 Ok\r\n" +
                                "Connection: close\r\n\r\n");
                            stream.Write(response, 0, response.Length);
                            var contentBytes = GetContent(url);
                            if (contentBytes != null)
                            {
                                stream.Write(contentBytes, 0, contentBytes.Length);
                            }
                            stream.Close();
                            return;
                        }
                    }
                }
                else if (state == HTTPState.WebSocket)
                {
                    bool fin = (bytes[0] & 0b10000000) != 0;
                    bool mask = (bytes[1] & 0b10000000) != 0; // must be true, "All messages from the client to the server have this bit set"

                    int opcode = bytes[0] & 0b00001111, // expecting 1 - text message
                        msglen = bytes[1] - 128, // & 0111 1111
                        offset = 2;

                    if (msglen == 126)
                    {
                        // was ToUInt16(bytes, offset) but the result is incorrect
                        msglen = BitConverter.ToUInt16(new byte[] { bytes[3], bytes[2] }, 0);
                        offset = 4;
                    }
                    else if (msglen == 127)
                    {
                        Console.WriteLine("TODO: msglen == 127, needs qword to store msglen");
                        // i don't really know the byte order, please edit this
                        // msglen = BitConverter.ToUInt64(new byte[] { bytes[5], bytes[4], bytes[3], bytes[2], bytes[9], bytes[8], bytes[7], bytes[6] }, 0);
                        // offset = 10;
                    }

                    if (msglen == 0)
                        Console.WriteLine("msglen == 0");
                    else if (mask)
                    {
                        byte[] decoded = new byte[msglen];
                        byte[] masks = new byte[4] { bytes[offset], bytes[offset + 1], bytes[offset + 2], bytes[offset + 3] };
                        offset += 4;

                        for (int i = 0; i < msglen; ++i)
                            decoded[i] = (byte)(bytes[offset + i] ^ masks[i % 4]);

                        string text = Encoding.UTF8.GetString(decoded);
                        Console.WriteLine("{0}", text);
                        this.queue.Enqueue(text);
                    }
                    else
                        Console.WriteLine("mask bit not set");

                    pos = read;
                }
            }
        }
    }

    private int FindHeaderEnd(byte[] bytes, int offset, int length, ref int searchIndex)
    {
        int pos = offset;
        int end = offset + length;
        while (pos < end)
        {
            if (searchIndex == 0)
            {
                pos = Array.IndexOf(bytes, HeaderEnd[0], pos, end - pos);
                if (pos == -1)
                {
                    return -1;
                }

                pos++;
                searchIndex++;
                if (searchIndex >= HeaderEnd.Length)
                {
                    return pos;
                }
            }
            else
            {
                if (bytes[pos] == HeaderEnd[searchIndex])
                {
                    pos++;
                    searchIndex++;
                    if (searchIndex >= HeaderEnd.Length)
                    {
                        return pos;
                    }
                }
                else
                {
                    pos -= searchIndex - 1;
                    searchIndex = 0;
                }
            }
        }

        return -1;
    }

    private byte[] GetContent(string url)
    {
        int pos = url.IndexOf('/');
        if (pos == -1) return null;
        string filename = url.Substring(pos + 1);
        if (filename == "") filename = "index.html";
        byte[] bytes;
        return this.contents.TryGetValue(filename, out bytes) ? bytes : null;
    }
}
