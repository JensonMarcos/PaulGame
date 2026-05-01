// using KinematicCharacterController;
// using Unity.Netcode;
// using UnityEngine;

// // Runs after KCC's KinematicCharacterSystem so we read & record post-motor state.
// [DefaultExecutionOrder(100)]
// [RequireComponent(typeof(Rigidbody))]
// public class PlayerNetworkTransform : NetworkBehaviour
// {
//     [Header("Reconciliation")]
//     [SerializeField, Tooltip("Errors below this (m) are treated as floating-point noise and ignored.")]
//     float ignoreThreshold = 0.005f;
//     [SerializeField, Tooltip("Errors above this (m) snap immediately. Should be reached only after major divergence (dropped inputs, big network hiccup).")]
//     float snapThreshold = 1f;
//     [SerializeField, Range(0f, 1f), Tooltip("Fraction of remaining error consumed each tick during smooth correction.")]
//     float correctionFactor = 0.3f;
//     [SerializeField, Tooltip("Hard cap on how far (m) the smooth correction may shift the owner per tick.")]
//     float maxCorrectionStep = 0.05f;

//     [Header("Remote Interpolation")]
//     [SerializeField, Tooltip("Visual delay (s) for non-owner clients. Should be larger than the server's tick interval.")]
//     float interpolationDelay = 0.05f;

//     [Header("Send Threshold")]
//     [SerializeField] float positionDeltaThreshold = 0.001f;
//     [SerializeField] float rotationDeltaThreshold = 0.1f;

//     [Header("Refs")]
//     [SerializeField] KinematicCharacterMotor motor;
//     [SerializeField] PlayerCharacter playerCharacter;

//     Rigidbody rb;

//     NetworkVariable<TransformState> serverState = new NetworkVariable<TransformState>(
//         writePerm: NetworkVariableWritePermission.Server,
//         readPerm: NetworkVariableReadPermission.Everyone
//     );

//     Vector3 lastSentPos;
//     Quaternion lastSentRot;
//     uint lastSentSeq;

//     struct TransformState : INetworkSerializable
//     {
//         public Vector3 Position;
//         public Quaternion Rotation;
//         public uint AckSequence;

//         public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
//         {
//             serializer.SerializeValue(ref Position);
//             serializer.SerializeValue(ref Rotation);
//             serializer.SerializeValue(ref AckSequence);
//         }
//     }

//     // --- Owner prediction buffer ---
//     // Indexed by (sequence % bufferSize). Stores the post-motor position the owner predicted for that input sequence.
//     const int bufferSize = 256;
//     Vector3[] predictedPositions = new Vector3[bufferSize];
//     uint[] predictedSequences = new uint[bufferSize];
//     Vector3 pendingError;

//     // --- Remote snapshot interpolation ---
//     struct Snapshot { public TransformState State; public float Time; }
//     Snapshot fromSnap, toSnap;
//     bool hasSnapshots;

//     void Awake()
//     {
//         rb = GetComponent<Rigidbody>();
//     }

//     public override void OnNetworkSpawn()
//     {
//         if(IsServer)
//         {
//             lastSentPos = rb.position;
//             lastSentRot = rb.rotation;
//             return;
//         }

//         if(!IsOwner)
//         {
//             // Non-authoritative copy: let physics smooth visual transform between FixedUpdates.
//             rb.interpolation = RigidbodyInterpolation.Interpolate;
//             var initial = new TransformState { Position = rb.position, Rotation = rb.rotation };
//             toSnap = new Snapshot { State = initial, Time = Time.time };
//             fromSnap = toSnap;
//             hasSnapshots = true;
//         }

//         serverState.OnValueChanged += OnServerStateChanged;
//     }

//     public override void OnNetworkDespawn()
//     {
//         if(!IsServer) serverState.OnValueChanged -= OnServerStateChanged;
//     }

//     void FixedUpdate()
//     {
//         // Server publishes its post-motor state with the last input sequence it applied.
//         if(IsServer) { ServerPublish(); return; }

//         if(IsOwner)
//         {
//             // Record this tick's predicted position against the input sequence the motor just used.
//             uint seq = playerCharacter.LatestInputSequence;
//             int slot = (int)(seq % bufferSize);
//             predictedSequences[slot] = seq;
//             predictedPositions[slot] = rb.position;

//             ApplyPendingCorrection();
//             return;
//         }

//         RemoteFollow();
//     }

//     void ServerPublish()
//     {
//         Vector3 pos = rb.position;
//         Quaternion rot = rb.rotation;
//         uint seq = playerCharacter.LatestInputSequence;

//         bool poseChanged = (pos - lastSentPos).sqrMagnitude >= positionDeltaThreshold * positionDeltaThreshold
//                            || Quaternion.Angle(rot, lastSentRot) >= rotationDeltaThreshold;
//         bool seqChanged = seq != lastSentSeq;
//         if(!poseChanged && !seqChanged) return;

//         lastSentPos = pos;
//         lastSentRot = rot;
//         lastSentSeq = seq;
//         serverState.Value = new TransformState { Position = pos, Rotation = rot, AckSequence = seq };
//     }

//     void OnServerStateChanged(TransformState _, TransformState s)
//     {
//         if(IsOwner) ReconcileOwner(s);
//         else AddRemoteSnapshot(s);
//     }

//     void ReconcileOwner(TransformState s)
//     {
//         // Look up what we predicted for the input sequence the server just applied.
//         // Comparing against this (not against the owner's current position) is what avoids the wall-clip:
//         // when both server and owner stop at the wall on the same input, error is 0 even though the
//         // owner is many ticks ahead of the server in real time.
//         int slot = (int)(s.AckSequence % bufferSize);
//         if(predictedSequences[slot] != s.AckSequence) return; // ack is too old or never recorded

//         Vector3 error = s.Position - predictedPositions[slot];
//         float errSq = error.sqrMagnitude;

//         if(errSq < ignoreThreshold * ignoreThreshold)
//         {
//             pendingError = Vector3.zero;
//             return;
//         }

//         if(errSq > snapThreshold * snapThreshold)
//         {
//             // Major divergence — apply the error as a one-shot offset to current position.
//             // Since error is computed at the ack tick, this is a true correction, not a backwards rubber-band.
//             ApplyOffset(error);
//             pendingError = Vector3.zero;
//             return;
//         }

//         // Small/medium error: smooth it in over multiple ticks (handled in FixedUpdate).
//         pendingError = error;
//     }

//     void ApplyPendingCorrection()
//     {
//         if(pendingError.sqrMagnitude < 1e-8f) return;

//         Vector3 step = pendingError * correctionFactor;
//         float stepMag = step.magnitude;
//         if(stepMag > maxCorrectionStep) step *= maxCorrectionStep / stepMag;

//         ApplyOffset(step);
//         pendingError -= step;
//     }

//     void ApplyOffset(Vector3 offset)
//     {
//         if(motor != null) motor.SetPosition(rb.position + offset);
//         else rb.position += offset;
//     }

//     void AddRemoteSnapshot(TransformState s)
//     {
//         fromSnap = toSnap;
//         toSnap = new Snapshot { State = s, Time = Time.time };
//         if(!hasSnapshots) { fromSnap = toSnap; hasSnapshots = true; }
//     }

//     void RemoteFollow()
//     {
//         if(!hasSnapshots) return;

//         float renderTime = Time.time - interpolationDelay;
//         float span = toSnap.Time - fromSnap.Time;
//         float t = span > 0.0001f ? Mathf.Clamp01((renderTime - fromSnap.Time) / span) : 1f;

//         Vector3 pos = Vector3.LerpUnclamped(fromSnap.State.Position, toSnap.State.Position, t);
//         Quaternion rot = Quaternion.SlerpUnclamped(fromSnap.State.Rotation, toSnap.State.Rotation, t);

//         rb.MovePosition(pos);
//         rb.MoveRotation(rot);
//     }
// }
