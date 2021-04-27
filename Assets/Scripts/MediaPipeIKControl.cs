using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class MediaPipeIKControl : MonoBehaviour
{
    private class PoseResult
    {
        public List<Vector3> poseLandmarks;
    }

    protected Animator animator;
    private PoseResult currentPose;
    public Transform hipRb;

    // Start is called before the first frame update
    void Start()
    {
        animator = GetComponent<Animator>();
    }

    // Update is called once per frame
    void Update()
    {
    }

    void OnAnimatorIK()
    {
        if (this.currentPose != null)
        {
            for (int landmarkIndex = 0; landmarkIndex < this.currentPose.poseLandmarks.Count; landmarkIndex++)
            {
                AvatarIKGoal goal;
                if (landmarkIndex == 15)
                {
                    goal = AvatarIKGoal.LeftHand;
                }
                else if (landmarkIndex == 16)
                {
                    goal = AvatarIKGoal.RightHand;
                }
                else if (landmarkIndex == 27)
                {
                    goal = AvatarIKGoal.LeftFoot;
                }
                else if (landmarkIndex == 28)
                {
                    goal = AvatarIKGoal.RightFoot;
                }
                else
                {
                    continue;
                }
                var position = this.currentPose.poseLandmarks[landmarkIndex];
                position.y = 1.0f - position.y;
                position.x = position.x - 0.5f;
                animator.SetIKPositionWeight(goal, 1);
                //animator.SetIKRotationWeight(AvatarIKGoal.RightHand, 1);
                animator.SetIKPosition(goal, position);
                //animator.SetIKRotation(AvatarIKGoal.RightHand, rotation);
            }
        }
    }

    public void OnPoseResults(string json)
    {
        this.currentPose = JsonUtility.FromJson<PoseResult>(json);
        var leftHipPosition = this.currentPose.poseLandmarks[23];
        var rightHipPosition = this.currentPose.poseLandmarks[24];
        var position = (leftHipPosition + rightHipPosition) / 2.0f;
        position.y = 1.0f - position.y;
        position.x = position.x - 0.5f;
        this.hipRb.position = position;
    }
}
