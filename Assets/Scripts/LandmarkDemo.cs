using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class LandmarkDemo : MonoBehaviour
{
    private class PoseResult
    {
        public List<Vector3> poseLandmarks;
    }


    public GameObject landmarkPrefab;
    private GameObject[] landmarkObjects;
    private PoseResult currentPose;

    // Start is called before the first frame update
    void Start()
    {
        this.landmarkObjects = new GameObject[33];
        for (int i = 0; i < 33; i++)
        {
            this.landmarkObjects[i] = Instantiate(landmarkPrefab, new Vector3(0, 0, 0), Quaternion.identity);
        }
    }

    // Update is called once per frame
    void Update()
    {
    }

    internal void OnPoseResults(string json)
    {
        this.currentPose = JsonUtility.FromJson<PoseResult>(json);
        for (int i = 0; i < this.currentPose.poseLandmarks.Count; i++)
        {
            var o = this.landmarkObjects[i];
            var p = this.currentPose.poseLandmarks[i];
            o.transform.position = p;
        }
    }
}
