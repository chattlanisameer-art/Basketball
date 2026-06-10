using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Hands;

namespace Hesi.Player
{
    /// <summary>
    /// Single source of truth for one hand's pose and velocity.
    /// Prefers the XR Hands subsystem (bare-hand tracking on Pico 4 Ultra);
    /// falls back to the controller pose when hands aren't tracked.
    ///
    /// Attach one to each hand anchor under the XR Origin's Camera Offset.
    /// Every other system (dribble, grab, shoot) reads hand state from here —
    /// nothing else touches XR APIs directly.
    /// </summary>
    public class VRHandTracker : MonoBehaviour
    {
        public enum Hand { Left, Right }

        [SerializeField] private Hand hand = Hand.Right;

        [Tooltip("Seconds of pose history used for velocity smoothing. " +
                 "Shorter = snappier throws, longer = more stable. 0.09 is the sweet spot.")]
        [SerializeField] private float velocityWindow = 0.09f;

        // ---- Public state, read by Dribble/Grab/Shoot systems ----
        public Vector3 PalmPosition { get; private set; }
        public Quaternion PalmRotation { get; private set; }
        /// <summary>Smoothed world-space velocity over the sampling window.</summary>
        public Vector3 Velocity { get; private set; }
        /// <summary>Instantaneous (single-frame) velocity. Use for contact response, not throws.</summary>
        public Vector3 InstantVelocity { get; private set; }
        /// <summary>0..1 — how closed the fist is (hand tracking) or grip axis (controller).</summary>
        public float GripAmount { get; private set; }
        public bool IsTracked { get; private set; }
        public bool UsingHandTracking { get; private set; }

        private XRHandSubsystem _handSubsystem;
        private InputDevice _controller;

        private struct PoseSample { public Vector3 pos; public float time; }
        private readonly Queue<PoseSample> _history = new Queue<PoseSample>(32);
        private Vector3 _lastPos;
        private bool _hasLastPos;

        private void Start()
        {
            var subsystems = new List<XRHandSubsystem>();
            SubsystemManager.GetSubsystems(subsystems);
            if (subsystems.Count > 0) _handSubsystem = subsystems[0];

            _controller = InputDevices.GetDeviceAtXRNode(
                hand == Hand.Left ? XRNode.LeftHand : XRNode.RightHand);
        }

        private void Update()
        {
            if (!UpdateFromHandSubsystem())
                UpdateFromController();

            transform.SetPositionAndRotation(PalmPosition, PalmRotation);
            UpdateVelocity();
        }

        private bool UpdateFromHandSubsystem()
        {
            if (_handSubsystem == null || !_handSubsystem.running) return false;

            XRHand xrHand = hand == Hand.Left ? _handSubsystem.leftHand : _handSubsystem.rightHand;
            if (!xrHand.isTracked) return false;

            var palm = xrHand.GetJoint(XRHandJointID.Palm);
            if (!palm.TryGetPose(out Pose palmPose)) return false;

            // Hand-subsystem poses are in XR Origin space → convert to world.
            Transform origin = transform.parent != null ? transform.parent : transform;
            PalmPosition = origin.TransformPoint(palmPose.position);
            PalmRotation = origin.rotation * palmPose.rotation;

            GripAmount = EstimateFingerCurl(xrHand);
            IsTracked = true;
            UsingHandTracking = true;
            return true;
        }

        /// <summary>Average curl of index+middle+ring tips toward the palm, mapped to 0..1.</summary>
        private float EstimateFingerCurl(XRHand xrHand)
        {
            float total = 0f; int count = 0;
            XRHandJointID[] tips = { XRHandJointID.IndexTip, XRHandJointID.MiddleTip, XRHandJointID.RingTip };
            foreach (var tipId in tips)
            {
                var tip = xrHand.GetJoint(tipId);
                if (tip.TryGetPose(out Pose tipPose))
                {
                    // Distance from tip to palm: ~0.11m open, ~0.04m closed fist.
                    float d = Vector3.Distance(tipPose.position,
                        xrHand.GetJoint(XRHandJointID.Palm).TryGetPose(out Pose p) ? p.position : tipPose.position);
                    total += Mathf.InverseLerp(0.11f, 0.04f, d);
                    count++;
                }
            }
            return count > 0 ? total / count : 0f;
        }

        private void UpdateFromController()
        {
            if (!_controller.isValid)
                _controller = InputDevices.GetDeviceAtXRNode(
                    hand == Hand.Left ? XRNode.LeftHand : XRNode.RightHand);

            UsingHandTracking = false;
            if (_controller.isValid &&
                _controller.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 pos) &&
                _controller.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion rot))
            {
                Transform origin = transform.parent != null ? transform.parent : transform;
                PalmPosition = origin.TransformPoint(pos);
                PalmRotation = origin.rotation * rot;
                _controller.TryGetFeatureValue(CommonUsages.grip, out float grip);
                GripAmount = grip;
                IsTracked = true;
            }
            else
            {
                IsTracked = false;
            }
        }

        private void UpdateVelocity()
        {
            float now = Time.time;

            if (_hasLastPos && Time.deltaTime > 0f)
                InstantVelocity = (PalmPosition - _lastPos) / Time.deltaTime;
            _lastPos = PalmPosition;
            _hasLastPos = true;

            _history.Enqueue(new PoseSample { pos = PalmPosition, time = now });
            while (_history.Count > 2 && now - _history.Peek().time > velocityWindow)
                _history.Dequeue();

            PoseSample oldest = _history.Peek();
            float dt = now - oldest.time;
            Velocity = dt > 0.001f ? (PalmPosition - oldest.pos) / dt : Vector3.zero;
        }
    }
}
