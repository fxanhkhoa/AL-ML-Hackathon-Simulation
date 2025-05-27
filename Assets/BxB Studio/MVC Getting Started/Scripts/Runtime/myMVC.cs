using MVC.Core;
using System;
using System.Collections;
using UnityEngine;
using MVC.Internal;

namespace Assets.BxB_Studio.MVC_Getting_Started.Scripts.Runtime
{
    public class myMVC : MonoBehaviour
    {

        [Tooltip("The Vehicle")]
        public Vehicle myVehicle;

        // Use this for initialization
        void Start()
        {
            Debug.Log("CALLED Start");
            Debug.Log(myVehicle);
        }

        private void FixedUpdate()
        {
            Debug.Log("CALLED FixedUpdate");
            Debug.Log(myVehicle.Engine.Power);
        }

        // Update is called once per frame
        void Update()
        {
            Debug.Log("CALLED Update");
            Debug.Log(myVehicle.Engine.Power);
        }
    }
}