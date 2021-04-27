const pose = new Pose({
    locateFile: (file) => {
        return `https://cdn.jsdelivr.net/npm/@mediapipe/pose/${file}`;
    }
});
pose.setOptions({
    upperBodyOnly: false,
    smoothLandmarks: true,
    minDetectionConfidence: 0.5,
    minTrackingConfidence: 0.5
});
var i = 0;
var data = [];

pose.onResults((results) => {
    var canvasCtx = context;
    canvasCtx.save();
    canvasCtx.clearRect(0, 0, canvasElement.width, canvasElement.height);
    canvasCtx.drawImage(
        results.image, 0, 0, canvasElement.width, canvasElement.height);
    drawConnectors(canvasCtx, results.poseLandmarks, POSE_CONNECTIONS,
        { color: '#00FF00', lineWidth: 4 });
    drawLandmarks(canvasCtx, results.poseLandmarks,
        { color: '#FF0000', lineWidth: 2 });
    canvasCtx.restore();

    websocket.send(JSON.stringify({ poseLandmarks: results.poseLandmarks }));
});

const wsUri = "ws://" + window.location.host + "/";
var websocket = new WebSocket(wsUri);


websocket.onopen = function (e) {
    writeToScreen("CONNECTED");
    doSend("WebSocket rocks");
};

websocket.onclose = function (e) {
    writeToScreen("DISCONNECTED");
};

websocket.onmessage = function (e) {
    writeToScreen("<span>RESPONSE: " + e.data + "</span>");
};

websocket.onerror = function (e) {
    writeToScreen("<span class=error>ERROR:</span> " + e.data);
};

function doSend(message) {
    //writeToScreen("SENT: " + message);
    websocket.send(message);
}

function writeToScreen(s) {
    textareaElement.value += s;
}


var videoElement = document.getElementById('video');
var canvasElement = document.getElementById('canvas');
var textareaElement = document.getElementById('textarea');
var context = canvasElement.getContext('2d');

const camera = new Camera(videoElement, {
    onFrame: async () => {
        await pose.send({ image: videoElement });
    },
    width: 320,
    height: 240
});
camera.start();
