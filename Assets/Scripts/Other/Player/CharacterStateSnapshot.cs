using System;
using Unity.Netcode;
using UnityEngine;

namespace ARPG.StateSync
{
    /// <summary>
    /// Transport-independent state sent by an authoritative character.
    /// Timestamps must use the same clock on sender and receiver.
    /// </summary>
    [Serializable]
    public struct CharacterStateSnapshot : INetworkSerializable
    {
        [Flags]
        public enum StateFlags : byte
        {
            None = 0,
            Grounded = 1 << 0,
            Jumping = 1 << 1,
            Attacking = 1 << 2,
            Dodging = 1 << 3,
            AirborneAction = 1 << 4
        }

        public uint Sequence;
        public double Timestamp;
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 Velocity;

        public int AnimatorStateHash;
        public float AnimatorNormalizedTime;
        public float Speed;
        public float InputX;
        public float InputY;
        public float JumpSpeed;
        public int HoldItemId;
        public byte EquipmentId;
        public StateFlags Flags;

        public bool HasFlag(StateFlags flag) => (Flags & flag) != 0;

        public static CharacterStateSnapshot Interpolate(
            CharacterStateSnapshot from,
            CharacterStateSnapshot to,
            double timestamp)
        {
            double duration = to.Timestamp - from.Timestamp;
            float t = duration <= double.Epsilon
                ? 1f
                : Mathf.Clamp01((float)((timestamp - from.Timestamp) / duration));

            bool useNewDiscreteState = t >= 0.5f;
            return new CharacterStateSnapshot
            {
                Sequence = useNewDiscreteState ? to.Sequence : from.Sequence,
                Timestamp = timestamp,
                Position = Vector3.LerpUnclamped(from.Position, to.Position, t),
                Rotation = Quaternion.SlerpUnclamped(from.Rotation, to.Rotation, t),
                Velocity = Vector3.LerpUnclamped(from.Velocity, to.Velocity, t),
                AnimatorStateHash = useNewDiscreteState ? to.AnimatorStateHash : from.AnimatorStateHash,
                AnimatorNormalizedTime = from.AnimatorStateHash == to.AnimatorStateHash
                    ? Mathf.LerpUnclamped(from.AnimatorNormalizedTime, to.AnimatorNormalizedTime, t)
                    : (useNewDiscreteState ? to.AnimatorNormalizedTime : from.AnimatorNormalizedTime),
                Speed = Mathf.LerpUnclamped(from.Speed, to.Speed, t),
                InputX = Mathf.LerpUnclamped(from.InputX, to.InputX, t),
                InputY = Mathf.LerpUnclamped(from.InputY, to.InputY, t),
                JumpSpeed = Mathf.LerpUnclamped(from.JumpSpeed, to.JumpSpeed, t),
                HoldItemId = useNewDiscreteState ? to.HoldItemId : from.HoldItemId,
                EquipmentId = useNewDiscreteState ? to.EquipmentId : from.EquipmentId,
                Flags = useNewDiscreteState ? to.Flags : from.Flags
            };
        }

        public static CharacterStateSnapshot Extrapolate(
            CharacterStateSnapshot snapshot,
            double timestamp,
            float maxSeconds)
        {
            float seconds = Mathf.Clamp((float)(timestamp - snapshot.Timestamp), 0f, maxSeconds);
            snapshot.Position += snapshot.Velocity * seconds;
            snapshot.Timestamp = timestamp;
            return snapshot;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Sequence);
            serializer.SerializeValue(ref Timestamp);
            serializer.SerializeValue(ref Position);
            serializer.SerializeValue(ref Rotation);
            serializer.SerializeValue(ref Velocity);
            serializer.SerializeValue(ref AnimatorStateHash);
            serializer.SerializeValue(ref AnimatorNormalizedTime);
            serializer.SerializeValue(ref Speed);
            serializer.SerializeValue(ref InputX);
            serializer.SerializeValue(ref InputY);
            serializer.SerializeValue(ref JumpSpeed);
            serializer.SerializeValue(ref HoldItemId);
            serializer.SerializeValue(ref EquipmentId);

            byte flags = (byte)Flags;
            serializer.SerializeValue(ref flags);
            if (serializer.IsReader)
                Flags = (StateFlags)flags;
        }
    }
}
