using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mediapipe.Unity.CoordinateSystem;
using Mediapipe.Unity.Holistic;
using Google.Protobuf;
using Mediapipe;
using Mediapipe.Unity;

public class MediaPipeFaceSolver : MonoBehaviour
{
    public struct Face
    {
        public Head Head;
        public Eyes Eyes;
        // It only has one value for the eye brows and pupil in the original code (Kaleido)
        // TODO: investigate getting 2 different brow and pupil values
        public float Brow;
        public Vector2 Pupils;
        public Mouth Mouth;
    }

    public struct Head
    {
        // X, Y and Z represent 3D radian angles.
        public float X;
        public float Y;
        public float Z;

        public float Width;
        public float Height;

        /// Center of face detection square.
        public Vector3 Position;
        // TODO: convert to a Quaternion
        // Euler angles normalized between -1 and 1.
        public Vector3 NormalizedAngles;
    }

    public struct Mouth
    {
        // Horizontal mouth open
        public float X;
        // Vertical mouth open
        public float Y;
        // Mouth vowel shape
        public Phoneme Shape;
    }

    public struct Phoneme
    {
        // 'A' shape.
        public float A;
        // 'E' shape.
        public float E;
        // 'I' shape.
        public float I;
        // 'O' shape.
        public float O;
        // 'U' shape.
        public float U;
    }

    public struct Eyes
    {
        // Wideness of left eye
        public float Left;
        /// Wideness of right eye
        public float Right;
    }

    public readonly int[] EyeLeftPoints = new int[] { 130, 133, 160, 159, 158, 144, 145, 153 };
    public readonly int[] EyeRightPoints = new int[] { 263, 362, 387, 386, 385, 373, 374, 380 };
    public readonly int[] BrowLeftPoints = new int[] { 35, 244, 63, 105, 66, 229, 230, 231 };
    public readonly int[] BrowRightPoints = new int[] { 265, 464, 293, 334, 296, 449, 450, 451 };
    public readonly int[] PupilLeftPoints = new int[] { 468, 469, 470, 471, 472 };
    public readonly int[] PupilRightPoints = new int[] { 473, 474, 475, 476, 477 };

    public Vector3 ToVector(Landmark landmark) => new(landmark.X, landmark.Y, landmark.Z);
    public Vector2 ToVector2(Vector3 vector) => new(vector.x, vector.y);

    public Vector3 ToVector(NormalizedLandmark landmark) => new(landmark.X, landmark.Y, landmark.Z);
    public Vector2 ToVector2(NormalizedLandmark landmark) => new(landmark.X, landmark.Y);

    public Face Solve(
        NormalizedLandmarkList list,
        bool smoothBlink = false,
        float blinkHigh = .35f,
        float blinkLow = .5f
    )
    {
        Head head = CalcHead(list);
        Mouth mouth = CalcMouth(list);

        Eyes eyes = CalcEyes(list, blinkHigh, blinkLow);

        if (smoothBlink)
            StabilizeBlink(ref eyes, head.Y);

        Vector2 pupils = CalcPupils(list);
        float brow = CalcBrow(list);

        return new Face
        {
            Head = head,
            Eyes = eyes,
            Brow = brow,
            Pupils = pupils,
            Mouth = mouth,
        };
    }

    public Mouth CalcMouth(NormalizedLandmarkList list)
    {
        var landmarks = list.Landmark;

        // Eye keypoints
        Vector3 eyeInnerCornerL = ToVector(landmarks[133]);
        Vector3 eyeInnerCornerR = ToVector(landmarks[362]);
        Vector3 eyeOuterCornerL = ToVector(landmarks[130]);
        Vector3 eyeOuterCornerR = ToVector(landmarks[263]);

        // Eye keypoint distances
        float eyeInnerDistance = Vector3.Distance(eyeInnerCornerL, eyeInnerCornerR);
        float eyeOuterDistance = Vector3.Distance(eyeOuterCornerL, eyeOuterCornerR);

        // Mouth keypoints
        Vector3 upperInnerLip = ToVector(landmarks[13]);
        Vector3 lowerInnerLip = ToVector(landmarks[14]);
        Vector3 mouthCornerLeft = ToVector(landmarks[61]);
        Vector3 mouthCornerRight = ToVector(landmarks[291]);

        // Mouth keypoint distances
        float mouthOpen = Vector3.Distance(upperInnerLip, lowerInnerLip);
        float mouthWidth = Vector3.Distance(mouthCornerLeft, mouthCornerRight);

        // Mouth open and mouth shape ratios
        float ratioY = mouthOpen / eyeInnerDistance;
        float ratioX = mouthWidth / eyeOuterDistance;

        // Normalize and scale mouth open
        ratioY = ratioY.Remap(0.15f, 0.7f);

        // Normalize and scale mouth shape
        ratioX = ratioX.Remap(0.45f, 0.9f);
        ratioX = (ratioX - 0.3f) * 2;

        float mouthX = ratioX;
        float mouthY = (mouthOpen / eyeInnerDistance).Remap(0.17f, 0.5f);

        float ratioI = Math.Clamp(mouthX.Remap(0, 1) * 2 * mouthY.Remap(0.2f, 0.7f), 0, 1);
        float ratioA = mouthY * 0.4f + mouthY * (1 - ratioI) * 0.6f;
        float ratioU = mouthY * (1 - ratioI).Remap(0, 0.3f) * 0.1f;
        float ratioE = ratioU.Remap(0.2f, 1) * (1 - ratioI) * 0.3f;
        float ratioO = (1 - ratioI) * mouthY.Remap(0.3f, 1) * 0.4f;

        return new Mouth
        {
            X = ratioX,
            Y = ratioY,
            Shape = new Phoneme
            {
                A = ratioA,
                E = ratioE,
                I = ratioI,
                O = ratioO,
                U = ratioU,
            },
        };
    }

    public Head CalcHead(NormalizedLandmarkList list)
    {
        // Find 3 vectors that form a plane to represent the head
        Vector3[] plane = FaceEulerPlane(list);
        Vector3 rotate = VectorExtensions.RollPitchYaw(plane[0], plane[1], plane[2]);
        // Find center of face detection box
        Vector3 midPoint = Vector3.Lerp(plane[0], plane[1], 0.5f);
        // Roughly find the dimensions of the face detection box
        float width = Vector3.Distance(plane[0], plane[1]);
        float height = Vector3.Distance(midPoint, plane[2]);

        // Flip
        rotate.x *= -1;
        rotate.y *= -1;

        return new Head
        {
            X = rotate.x * MathF.PI,
            Y = rotate.y * MathF.PI,
            Z = rotate.z * MathF.PI,
            Width = width,
            Height = height,
            Position = Vector3.Lerp(midPoint, plane[2], 0.5f),
            NormalizedAngles = new Vector3(rotate.x, rotate.y, rotate.z),
        };
    }

    public Vector3[] FaceEulerPlane(NormalizedLandmarkList list)
    {
        // TODO: This vector processing could probably be optimised
        var landmarks = list.Landmark;

        // Create face detection square bounds
        Vector3 topLeft = ToVector(landmarks[21]);
        Vector3 topRight = ToVector(landmarks[251]);

        Vector3 bottomRight = ToVector(landmarks[397]);
        Vector3 bottomLeft = ToVector(landmarks[172]);

        Vector3 bottomMidpoint = Vector3.Lerp(bottomRight, bottomLeft, 0.5f);

        return new Vector3[] { topLeft, topRight, bottomMidpoint };
    }

    public float GetEyeOpen(NormalizedLandmarkList list, string side, float high = .85f, float low = .55f)
    {
        var landmarks = list.Landmark;

        int[] eyePoints = side == "right" ? EyeRightPoints : EyeLeftPoints;
        float eyeDistance = EyeLidRatio(
            landmarks[eyePoints[0]],
            landmarks[eyePoints[1]],
            landmarks[eyePoints[2]],
            landmarks[eyePoints[3]],
            landmarks[eyePoints[4]],
            landmarks[eyePoints[5]],
            landmarks[eyePoints[6]],
            landmarks[eyePoints[7]]
        );

        // Human eye width to height ratio is roughly .3
        float maxRatio = 0.285f;
        // Compare ratio against max ratio
        float ratio = Math.Clamp(eyeDistance / maxRatio, 0, 2);
        // Remap eye open and close ratios to increase sensitivity
        float eyeOpenRatio = ratio.Remap(low, high);

        return eyeOpenRatio;
    }

    public float EyeLidRatio(
        NormalizedLandmark outerCorner,
        NormalizedLandmark innerCorner,
        NormalizedLandmark outerUpperLid,
        NormalizedLandmark midUpperLid,
        NormalizedLandmark innerUpperLid,
        NormalizedLandmark outerLowerLid,
        NormalizedLandmark midLowerLid,
        NormalizedLandmark innerLowerLid)
    {
        Vector2 eyeOuterCorner = ToVector2(outerCorner);
        Vector2 eyeInnerCorner = ToVector2(innerCorner);

        Vector2 eyeOuterUpperLid = ToVector2(outerUpperLid);
        Vector2 eyeMidUpperLid = ToVector2(midUpperLid);
        Vector2 eyeInnerUpperLid = ToVector2(innerUpperLid);

        Vector2 eyeOuterLowerLid = ToVector2(outerLowerLid);
        Vector2 eyeMidLowerLid = ToVector2(midLowerLid);
        Vector2 eyeInnerLowerLid = ToVector2(innerLowerLid);

        // Use 2D Distances instead of 3D for less jitter
        float eyeWidth = Vector2.Distance(eyeOuterCorner, eyeInnerCorner);
        float eyeOuterLidDistance = Vector2.Distance(eyeOuterUpperLid, eyeOuterLowerLid);
        float eyeMidLidDistance = Vector2.Distance(eyeMidUpperLid, eyeMidLowerLid);
        float eyeInnerLidDistance = Vector2.Distance(eyeInnerUpperLid, eyeInnerLowerLid);
        float eyeLidAvg = (eyeOuterLidDistance + eyeMidLidDistance + eyeInnerLidDistance) / 3;
        float ratio = eyeLidAvg / eyeWidth;

        return ratio;
    }

    // Calculates pupil position [-1, 1].
    public Vector2 PupilPos(NormalizedLandmarkList list, string side)
    {
        var landmarks = list.Landmark;

        int[] eyePoints = side == "right" ? EyeRightPoints : EyeLeftPoints;
        Vector3 eyeOuterCorner = ToVector(landmarks[eyePoints[0]]);
        Vector3 eyeInnerCorner = ToVector(landmarks[eyePoints[1]]);
        float eyeWidth = Vector2.Distance(ToVector2(eyeOuterCorner), ToVector2(eyeInnerCorner));
        Vector3 midPoint = Vector3.Lerp(eyeOuterCorner, eyeInnerCorner, .5f);

        int[] pupilPoints = side == "right" ? PupilRightPoints : PupilLeftPoints;
        Vector3 pupil = ToVector(landmarks[pupilPoints[0]]);
        float dx = midPoint.x - pupil.x;
        float dy = midPoint.y - pupil.y - eyeWidth * .075f;

        float ratioX = 4 * dx / (eyeWidth / 2);
        float ratioY = 4 * dy / (eyeWidth / 4);

        return new Vector2(ratioX, ratioY);
    }

    public void StabilizeBlink(ref Eyes eyes, float headY, bool enableWink = true, float maxRotation = .5f)
    {
        eyes.Left = Math.Clamp(eyes.Left, 0, 1);
        eyes.Right = Math.Clamp(eyes.Right, 0, 1);

        // Difference between each eye
        float blinkDiff = MathF.Abs(eyes.Left - eyes.Right);
        // Threshold to which difference is considered a wink
        float blinkThresh = enableWink ? .8f : 1.2f;

        bool isClosing = eyes.Left < .3f && eyes.Right < .3f;
        bool isOpening = eyes.Left > .6f && eyes.Right > .6f;

        // Sets obstructed eye to the opposite eye value
        if (headY > maxRotation)
        {
            eyes.Left = eyes.Right;
            return;
        }
        if (headY < -maxRotation)
        {
            eyes.Right = eyes.Left;
            return;
        }

        // Wink of averaged blink values
        if (!(blinkDiff >= blinkThresh && !isClosing && !isOpening))
        {
            float value = Mathf.Lerp(eyes.Right, eyes.Left, eyes.Right > eyes.Left ? .95f : .05f);
            eyes.Left = value;
            eyes.Right = value;
        }
    }

    // Calculate eyes.
    public Eyes CalcEyes(NormalizedLandmarkList list, float high = .85f, float low = .55f)
    {
        var landmarks = list.Landmark;

        // Return early if no iris tracking
        if (landmarks.Count != 478)
        {
            return new Eyes
            {
                Left = 1,
                Right = 1,
            };
        }

        // Open [0, 1]
        return new Eyes
        {
            Left = GetEyeOpen(list, "left", high, low),
            Right = GetEyeOpen(list, "right", high, low),
        };
    }

    // Calculate pupil location normalized to eye bounds
    public Vector2 CalcPupils(NormalizedLandmarkList list)
    {
        var landmarks = list.Landmark;

        // Pupil (x: [-1, 1], y: [-1, 1])
        if (landmarks.Count != 478)
        {
            return new Vector2(0, 0);
        }

        // Track pupils using left eye
        Vector2 pupilLeft = PupilPos(list, "left");
        Vector2 pupilRight = PupilPos(list, "right");

        return (pupilLeft + pupilRight) * .5f;
    }

    /// Calculate brow raise
    public float GetBrowRaise(NormalizedLandmarkList list, string side)
    {
        var landmarks = list.Landmark;

        int[] browPoints = side == "right" ? BrowRightPoints : BrowLeftPoints;
        float browDistance = EyeLidRatio(
            landmarks[browPoints[0]],
            landmarks[browPoints[1]],
            landmarks[browPoints[2]],
            landmarks[browPoints[3]],
            landmarks[browPoints[4]],
            landmarks[browPoints[5]],
            landmarks[browPoints[6]],
            landmarks[browPoints[7]]
        );

        float maxBrowRatio = 1.15f;
        float browHigh = .125f;
        float browLow = .07f;
        float browRatio = browDistance / maxBrowRatio - 1;
        float browRaiseRatio = (Math.Clamp(browRatio, browLow, browHigh) - browLow) / (browHigh - browLow);

        return browRaiseRatio;
    }

    // Take the average of left and right eyebrow raise
    public float CalcBrow(NormalizedLandmarkList list)
    {
        var landmarks = list.Landmark;

        if (landmarks.Count != 478)
            return 0;

        float leftBrow = GetBrowRaise(list, "left");
        float rightBrow = GetBrowRaise(list, "right");

        return (leftBrow + rightBrow) / 2;
    }
}
