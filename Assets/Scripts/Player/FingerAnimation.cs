using UnityEngine;
using System.Collections;

[System.Serializable]
public class Finger
{
    public Transform tip;
    public int jointCount = 3;

    public Transform[] joints;
    public bool[] jointDone;
    public float[] jointRot;

    public bool fingerDone;

    public void Init()
    {
        joints = new Transform[jointCount];
        jointDone = new bool[jointCount];
        jointRot = new float[jointCount];

        for (int i = jointCount-1; i >= 0; i--)
        {
            if(i == jointCount-1) 
                joints[i] = tip.parent;
            else
                joints[i] = joints[i+1].parent;
        }
    }


    public void ResetCurl()
    {
        for (int i = 0; i < jointRot.Length; i++)
            jointRot[i] = 0f;
    }
}

public class FingerAnimation : MonoBehaviour
{
    [Header("Fingers")]
    public Finger[] fingers;

    [Header("Grip Settings")]
    public LayerMask gunLayer;
    public float maxCurlDegrees = 90f;   // Max rotation per joint
    public float stepDegrees = 2f;        // Degrees per iteration step
    public float probeRadius = 0.005f;

    bool gripping;

    public void Initialize()
    {
        foreach (var finger in fingers) finger.Init();
    }

    public void GripGun()
    {
        ResetFingers();
        gripping = true;
    }

    public void ResetFingers()
    {
        foreach (var finger in fingers)
        {
            finger.ResetCurl();
            finger.fingerDone = false;

            for (int j = 0; j < finger.jointCount; j++)
            {
                finger.jointDone[j] = false;
                finger.jointRot[j] = 0f;
            }
        }
        gripping = false;
    }

    public void UpdateFingers()
    {
        if(!gripping) return;

        for (int i = 0; i < fingers.Length; i++)
        {
            Finger finger = fingers[i];

            for(int j = 0; j < finger.jointCount; j++)
            {   
                finger.joints[j].localEulerAngles = new Vector3(finger.jointRot[j], finger.joints[j].localEulerAngles.y, finger.joints[j].localEulerAngles.z);
            }

            //curl joints
            if (finger.fingerDone) continue;

            bool fingerMoving = false;

            for(int j = 0; j < finger.jointCount; j++)
            {   
                if(finger.jointDone[j] || (j > 0 && !finger.jointDone[j-1])) continue;


                finger.jointRot[j] += stepDegrees;
                finger.joints[j].localEulerAngles = new Vector3(finger.jointRot[j], finger.joints[j].localEulerAngles.y, finger.joints[j].localEulerAngles.z);

                if(j >= finger.jointCount-1) {
                    if(JointHit(finger.tip, probeRadius)) finger.jointDone[j] = true;
                }
                else {
                    if(JointHit(finger.joints[j+1], probeRadius)) finger.jointDone[j] = true;
                }
        
                if(finger.jointRot[j] >= maxCurlDegrees) finger.jointDone[j] = true;

                if(!finger.jointDone[j]) fingerMoving = true;
            }

            if(!fingerMoving) finger.fingerDone = true;
            
        }
    }

    bool JointHit(Transform joint, float radius)
    {
        return Physics.CheckSphere(joint.position, radius, gunLayer, QueryTriggerInteraction.Ignore);
    }
}
