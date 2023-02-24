# MediaPipe-UnitySolver
### Face, Pose, and Hand Tracking Solver for MediaPipeUnity Plugin

Library converting [Moetion](https://github.com/vignetteapp/Moetion) (Inspired by [KalidoKit](https://github.com/yeemachine/kalidokit)) types to work within Unity for use with [MediaPipeUnityPlugin](https://github.com/homuler/MediaPipeUnityPlugin) landmark outputs.

UnitySolver is composed of 3 classes for Face and Pose solving. Hand solving to be added later.

## 🦆 Usage

### NOTE: Pose solver needs a lot of work, not in a usable state with Mixamo/RPM rigs. 🛠

### Pose
```c#
// Give NormalizedLandmarkList from calculator as parameter for MediaPipePoseSolver solve function
var pose = _poseSolver.Solve(list);

SetEuler(LeftHand, pose.LeftArm.Hand, 1f, 0.3f);
SetEuler(RightHand, pose.RightArm.Hand, 1f, 0.3f);

SetEuler(LeftLowerArm, pose.LeftArm.Lower, 1f, 0.3f);
SetEuler(LeftUpperArm, pose.LeftArm.Upper, 1f, 0.3f);

SetEuler(RightLowerArm, pose.RightArm.Lower, 1f, 0.3f);
SetEuler(RightUpperArm, pose.RightArm.Upper, 1f, 0.3f);

SetEuler(Hips, pose.Hips.Rotation, 0.4f, 0.1f, true);
SetEuler(Spine, pose.Spine, 0.3f, 0.1f, true);
```

```c#
private void SetEuler(Transform rigPart, Vector3 target, float dampener = 1, float lerpAmount = 0.3f, bool flipped = false)
{
    // Convert radians to degrees
    Vector3 toDegrees = new(target.x * Mathf.Rad2Deg, target.y * Mathf.Rad2Deg, target.z * Mathf.Rad2Deg);
    Vector3 euler = new(toDegrees.x * dampener, toDegrees.y * dampener, toDegrees.z * dampener);

    if (flipped)
    {
        Quaternion quat = Quaternion.Euler(-euler);
        rigPart.localRotation = Quaternion.Slerp(rigPart.localRotation, quat, lerpAmount);
    }
    else
    {
        Quaternion quat = Quaternion.Euler(euler);
        rigPart.localRotation = Quaternion.Slerp(rigPart.localRotation, quat, lerpAmount);
    }
}
```
#### Pose Output
```c#
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
```
### Face
```c#
// Give NormalizedLandmarkList from calculator as parameter for MediaPipeFaceSolver solve function
var face = _faceSolver.Solve(list);
```
#### Face Output
```c#
public struct Face
    {
        public Head Head;
        public Eyes Eyes;
        public float Brow;
        public Vector2 Pupils;
        public Mouth Mouth;
    }

    public struct Head
    {
        public float X;
        public float Y;
        public float Z;

        public float Width;
        public float Height;
        
        public Vector3 Position;
        public Vector3 NormalizedAngles;
    }

    public struct Mouth
    {
        public float X;
        public float Y;
        public Phoneme Shape;
    }

    public struct Phoneme
    {
        public float A;
        public float E;
        public float I;
        public float O;
        public float U;
    }

    public struct Eyes
    {
        // Wideness of eyes
        public float Left;
        public float Right;
    }
```

### :ghost: Contribute
This library is a work in progress and contributions to improve it are very welcome.
```bash
git clone https://github.com/BrandonBartram98/MediaPipe-UnitySolver
```
