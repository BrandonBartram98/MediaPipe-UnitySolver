using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mediapipe.Unity.CoordinateSystem;
using Mediapipe.Unity.Holistic;
using Google.Protobuf;
using Mediapipe;
using Mediapipe.Unity;

public class MediaPipePoseSolver : MonoBehaviour
{
    public struct Pose
    {
        public Arm LeftArm;
        public Arm RightArm;
        public Leg LeftLeg;
        public Leg RightLeg;
        public Vector3 Spine;
        public Hips Hips;
    }

    public struct Arm
    {
        public Vector3 Upper;
        public Vector3 Lower;
        public Vector3 Hand;
    }

    public struct Leg
    {
        public Vector3 Upper;
        public Vector3 Lower;
    }

    public struct Hips
    {
        public Vector3 WorldPosition;
        public Vector3 Position;
        public Vector3 Rotation;
    }

    public Pose Solve(NormalizedLandmarkList normalizedLandmarkList)
    {
        (Arm leftArm, Arm rightArm) = CalculateArms(normalizedLandmarkList);
        (Hips hips, Vector3 spine) = CalculateHips(normalizedLandmarkList);

        var pose = new Pose
        {
            LeftArm = leftArm,
            RightArm = rightArm,
            Hips = hips,
            Spine = spine
        };

        return pose;
    }

    public Vector3 ToVector(NormalizedLandmark landmark) => new(landmark.X, landmark.Y, landmark.Z);
    public Vector2 ToVector2(Vector3 vector) => new (vector.x, vector.y );

    private (Arm, Arm) CalculateArms(NormalizedLandmarkList normalizedLandmarkList)
    {
        var landmarks = normalizedLandmarkList.Landmark;

        var rightArm = new Arm();
        var leftArm = new Arm();

        rightArm.Upper = VectorExtensions.FindRotation(ToVector(landmarks[11]), ToVector(landmarks[13]), true);
        leftArm.Upper = VectorExtensions.FindRotation(ToVector(landmarks[12]), ToVector(landmarks[14]), true);
        rightArm.Upper.y = VectorExtensions.AngleBetween3DCoords(ToVector(landmarks[12]), ToVector(landmarks[11]), ToVector(landmarks[13]));
        leftArm.Upper.y = VectorExtensions.AngleBetween3DCoords(ToVector(landmarks[11]), ToVector(landmarks[12]), ToVector(landmarks[14]));

        rightArm.Lower = VectorExtensions.FindRotation(ToVector(landmarks[13]), ToVector(landmarks[15]), true);
        leftArm.Lower = VectorExtensions.FindRotation(ToVector(landmarks[14]), ToVector(landmarks[16]), true);
        rightArm.Lower.y = VectorExtensions.AngleBetween3DCoords(ToVector(landmarks[11]), ToVector(landmarks[13]), ToVector(landmarks[15]));
        leftArm.Lower.y = VectorExtensions.AngleBetween3DCoords(ToVector(landmarks[12]), ToVector(landmarks[14]), ToVector(landmarks[16]));
        rightArm.Lower.z = Math.Clamp(rightArm.Lower.z, -2.14f, 0f);
        leftArm.Lower.z = Math.Clamp(leftArm.Lower.z, -2.14f, 0f);

        rightArm.Hand = VectorExtensions.FindRotation(ToVector(landmarks[15]), Vector3.Lerp(ToVector(landmarks[17]), ToVector(landmarks[19]), .5f), true);
        leftArm.Hand = VectorExtensions.FindRotation(ToVector(landmarks[16]), Vector3.Lerp(ToVector(landmarks[18]), ToVector(landmarks[20]), .5f), true);

        // Modify rotations slightly for more natural movement
        RigArm(ref rightArm, "right");
        RigArm(ref leftArm, "left");

        return (leftArm, rightArm);
    }

    private void RigArm(ref Arm arm, string side)
    {
        float invert = side == "right" ? 1f : -1f;

        arm.Upper.z *= -2.3f * invert;

        arm.Upper.y *= MathF.PI * invert;
        arm.Upper.y -= Math.Max(arm.Lower.x, 0);
        arm.Upper.y -= -invert * Math.Max(arm.Lower.z, 0);
        arm.Upper.x -= 0.3f * invert;

        arm.Lower.z *= -2.14f * invert;
        arm.Lower.y *= 2.14f * invert;
        arm.Lower.x *= 2.14f * invert;

        // Clamp values to realistic humanoid limits
        arm.Upper.x = Math.Clamp(arm.Upper.x, -0.5f, MathF.PI);
        arm.Lower.x = Math.Clamp(arm.Lower.x, -0.3f, 0.3f);

        arm.Hand.y = Math.Clamp(arm.Hand.z * 2, -0.6f, 0.6f); // sides
        arm.Hand.z = arm.Hand.z * -2.3f * invert; // up and down
    }

    private (Hips, Vector3) CalculateHips(NormalizedLandmarkList list)
    {
        var landmarks = list.Landmark;

        // Find 2D normalized hip and shoulder joint positions / distances
        Vector2 hipLeft2d = ToVector2(ToVector(landmarks[23]));
        Vector2 hipRight2d = ToVector2(ToVector(landmarks[24]));
        Vector2 shoulderLeft2d = ToVector2(ToVector(landmarks[11]));
        Vector2 shoulderRight2d = ToVector2(ToVector(landmarks[12]));

        Vector2 hipCenter2d = Vector2.Lerp(hipLeft2d, hipRight2d, 1);
        Vector2 shoulderCenter2d = Vector2.Lerp(shoulderLeft2d, shoulderRight2d, 1);
        float spineLength = Vector2.Distance(hipCenter2d, shoulderCenter2d);

        var hips = new Hips
        {
            Position = new Vector3(Math.Clamp(-1 * (hipCenter2d.x - .65f), -1, 1), 0, Math.Clamp(spineLength - 1, -2, 0)),
            Rotation = VectorExtensions.RollPitchYaw(ToVector(landmarks[23]), ToVector(landmarks[24])),
        };

        if (hips.Rotation.y > .5f)
            hips.Rotation.y -= 2;

        hips.Rotation.y += .5f;

        //Stop jumping between left and right shoulder tilt
        if (hips.Rotation.z > 0)
            hips.Rotation.z = 1 - hips.Rotation.z;

        if (hips.Rotation.z < 0)
            hips.Rotation.z = -1 - hips.Rotation.z;

        float turnAroundAmountHips = Math.Abs(hips.Rotation.y).Remap(.2f, .4f);
        hips.Rotation.z *= 1 - turnAroundAmountHips;
        hips.Rotation.x = 0; // Temp fix for inaccurate X axis

        Vector3 spine = VectorExtensions.RollPitchYaw(ToVector(landmarks[11]), ToVector(landmarks[12]));

        if (spine.y > .5f)
            spine.y -= 2;

        spine.y += .5f;

        // Prevent jumping between left and right shoulder tilt
        if (spine.z > 0)
            spine.z = 1 - spine.z;

        if (spine.z < 0)
            spine.z = -1 - spine.z;

        // Fix weird large numbers when 2 shoulder points get too close
        float turnAroundAmount = Math.Abs(spine.y).Remap(.2f, .4f);
        spine.z *= 1 - turnAroundAmount;
        spine.x = 0; // Temp fix for inaccurate X axis

        RigHips(ref hips, ref spine);
        return (hips, spine);
    }

    private void RigHips(ref Hips hips, ref Vector3 spine)
    {
        // Convert normalized values to radians
        hips.Rotation *= MathF.PI;

        hips.WorldPosition = new Vector3
        (
            hips.Position.x * (.5f + 1.8f * -hips.Position.z),
            0,
            hips.Position.z * (.1f + hips.Position.z * -2)
        );

        spine *= MathF.PI;
    }
}
