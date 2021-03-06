﻿using MLAgents;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DroneDirectionAgent : Agent {

    public GameObject Target;
    public GameObject FLThruster;
    public GameObject FRThruster;
    public GameObject RLThruster;
    public GameObject RRThruster;

    public float StartingHeightMin = -1f;
    public float StartingHeightMax = 1f;
    public float GraceDistance = 0.1f;
    public float UpForce = 10f;
    public float RewardScale = 1f;
    public float EnergyRewardScale = -0.01f;
    public bool Visualize = true;

    Rigidbody MarkerRigidBody;
    float previousDistance = 0f;

    Vector3 startPosition;
    BehaviorParameters behaviorParams;

    private void Start()
    {
        MarkerRigidBody = gameObject.GetComponent<Rigidbody>();
        behaviorParams = GetComponent<BehaviorParameters>();
        startPosition = transform.position;
    }

    // How to reinitialize when the game is reset. The Start() of an ML Agent
    public override void AgentReset()
    {
        if (MarkerRigidBody != null)
        {
            MarkerRigidBody.velocity = Vector3.zero;
            MarkerRigidBody.rotation = Quaternion.identity;
        }

        transform.position = new Vector3(Random.Range(StartingHeightMin, StartingHeightMax), Random.Range(StartingHeightMin, StartingHeightMax), Random.Range(StartingHeightMin, StartingHeightMax)) + startPosition;
        Target.transform.position = new Vector3(Random.Range(StartingHeightMin, StartingHeightMax), Random.Range(StartingHeightMin, StartingHeightMax), Random.Range(StartingHeightMin, StartingHeightMax)) + startPosition;
        previousDistance = Vector3.Distance(transform.position, Target.transform.position);

        if (Visualize)
        {
            for (var y = -2f; y < 2f; y += 0.05f)
            {
                var reward = CalculateReward(y, Target.transform.position.y, 0f);
                var c = Color.Lerp(Color.black, Color.white, reward * 5);
                Debug.DrawLine(new Vector3(transform.position.x, y, transform.position.z), new Vector3(transform.position.x, y + 0.05f, transform.position.z), c, 30);
            }
        }
    }

    // Tell the ML algorithm everything you can about the current state
    public override void CollectObservations()
    {
        var heading = Target.transform.position - transform.position;
        var distance = heading.magnitude;
        var direction = heading / distance;
        AddVectorObs(direction); // distance to target
        AddVectorObs(MarkerRigidBody.velocity); // Current velocity
        AddVectorObs(transform.rotation); // Quaternion
    }


    // What to do every step. The Update() of an ML Agent
    public override void AgentAction(float[] actions)
    {
        // This example only uses continuous space
        if (behaviorParams.brainParameters.vectorActionSpaceType != SpaceType.Continuous)
        {
            Debug.LogError("Must be continuous state type");
            return;
        }

        float actionFR = Mathf.Clamp(actions[0], -1f, 1f) * UpForce; // One of many actions
        float actionFL = Mathf.Clamp(actions[1], -1f, 1f) * UpForce;
        float actionRR = Mathf.Clamp(actions[2], -1f, 1f) * UpForce;
        float actionRL = Mathf.Clamp(actions[3], -1f, 1f) * UpForce;

        // Calculate the absolute value of the energy used
        var energyUsed = (Mathf.Abs(actionFR) + Mathf.Abs(actionFL) + Mathf.Abs(actionRR) + Mathf.Abs(actionRL)) / UpForce;

        if (MarkerRigidBody != null)
        {
            MarkerRigidBody.AddForceAtPosition(transform.up * actionFR, FRThruster.transform.position);
            MarkerRigidBody.AddForceAtPosition(transform.up * actionFL, FLThruster.transform.position);
            MarkerRigidBody.AddForceAtPosition(transform.up * actionRR, RRThruster.transform.position);
            MarkerRigidBody.AddForceAtPosition(transform.up * actionRL, RLThruster.transform.position);
        }

        var reward = CalculateReward(transform.position.y, Target.transform.position.y, energyUsed);
        SetReward(reward);
    }

    public float CalculateReward(float markerY, float targetY, float energyUsed)
    {
        var distance = Vector3.Distance(transform.position, Target.transform.position);
        var improvement = previousDistance - distance;
        if (distance < GraceDistance) improvement = Mathf.Max(0f, improvement); // If within a grace area, it's still better than nothing
        previousDistance = distance;

        var improvementPortion = RewardScale * improvement;
        var energyPortion = EnergyRewardScale * energyUsed;

        return improvementPortion + energyPortion;
    }
}
