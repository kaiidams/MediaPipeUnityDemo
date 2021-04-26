using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class LandmarkDemo : MonoBehaviour
{
    public GameObject landmarkPrefab;
    private List<List<Vector3>> landmarkData;
    private GameObject[] landmarkObjects;
    private string json;
    private int step;

    // Start is called before the first frame update
    void Start()
    {
        string textData = ((TextAsset)Resources.Load("landmarks")).text;
        this.landmarkData = new List<List<Vector3>>();
        using (var reader = new StringReader(textData))
        {
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                string[] parts = line.Split();
                var p = new Vector3(
                    float.Parse(parts[0]),
                    2-float.Parse(parts[1]),
                    float.Parse(parts[2])
                    );
                if (landmarkData.Count == 0 || landmarkData[landmarkData.Count - 1].Count >= 33)
                {
                    landmarkData.Add(new List<Vector3>());
                }
                landmarkData[landmarkData.Count - 1].Add(p);
            }
        }
        this.landmarkObjects = new GameObject[33];
        for (int i = 0; i < 33; i++)
        {
            this.landmarkObjects[i] = Instantiate(landmarkPrefab, new Vector3(0, 0, 0), Quaternion.identity);
        }
        step = 0;
    }

    // Update is called once per frame
    void Update()
    {
        for (int i = 0; i < this.landmarkObjects.Length; i++)
        {
            var o = this.landmarkObjects[i];
            var p = this.landmarkData[step / 10][i];
            o.transform.position = p;
        }
        step++;
        if (step >= this.landmarkData.Count * 10)
        {
            step = 0;
        }
    }

    internal void OnPoseResults(string text)
    {
        throw new NotImplementedException();
    }
}
