﻿/// <summary>
/// Note!
/// 
/// === Unity's Input.GetTouches ===
/// 
/// This method returns a floating point position. And USUALLY the current position becomes exactly the next touch's previous position while moving :
/// [State] [currentX] [currentY] [prevX] [prevY]
/// Unity I  Down 994 99
/// Unity I  Move 990.7097 102.2903 994 99
/// Unity I  Move 991 103.6232 990.7097 102.2903
/// Unity I  Move 991 104.5 991 103.6232
/// Unity I  Move 991 105 991 104.5
/// Unity I  Move 991 106 991 105
/// 
/// But things got weird on multitouch. I found this log :
/// 04-10 15:48:12.269  24175    24216                  Unity I  Move 997 108 996 108
/// 04-10 15:48:12.302  24175    24216                  Unity I  Down 1309 295
/// 04-10 15:48:12.320  24175    24216                  Unity I  Move 997.6622 108 997 108
/// 04-10 15:48:12.320  24175    24216                  Unity I  Move 1310 295 1309 295
/// 04-10 15:48:12.338  24175    24216                  Unity I  Move 1311.207 296.2072 1310 295
/// 04-10 15:48:12.437  24175    24216                  Unity I  Move 1312 296 1311.207 296.2072
/// 04-10 15:48:12.486  24175    24216                  Unity I  Move 1313 296 1312 296
/// 04-10 15:48:12.789  24175    24216                  Unity I  Move 1314 296 1313 296
/// 04-10 15:48:12.916  24175    24216                  Unity I  Move 998 107 998 108
/// 
/// You see the 2nd finger down to coordinate 1309,295? I move that new finger around and then somehow when I try to move the first finger, the previous position supposed to be at X = 997.6622 but it becames 998! It seems that sub-pixel movement was captured but it's storage as a previous position is not reliable (the 997 -> 997.6622 movement)
/// 
/// This is the reason I put rounding in this class. So if you feed the class Unity's touch it would work.
/// 
/// That is not all, now the inverse :
/// 
/// Unity I  Move 1079.852 111.1885 1021.982 76.71417
/// Unity I  Move 633.6176 121 632.0875 121
/// Unity I  Move 1143.469 140.8815 1079.852 111.1885
/// Unity I  Move 635.1525 121 633.6176 121
/// Unity I  Move 1199.488 163.1343 1143.469 140.8815
/// Unity I  Move 638 121 635.1525 121
/// Unity I  Up 1233 174 1199.488 163.1343
/// Unity I  Up 639.5 121 638.2614 121
/// 
/// The last up 638.2614 gets +0.2614 out of nowhere! The previous point is supposed to be just 638
/// This means the floating point is not guaranteed to connect in both ways. We also needs rounding on the input.
/// 
/// === iOS Native Touch ===
/// 
/// As said in http://exceed7.com/ios-native-touch/how-to-use.html
/// This sequence is possible
/// 
/// Moved 194 127 207 217
/// Moved 190 156 194 127
/// Ended 190 156 194 127
/// 
/// The usual would have been "Ended something something 190 156" where it connects with previous position.
/// I don't know why iOS sometimes output this behaviour, but a band aid fix is already in this code.
/// 
/// </summary>


//Uncomment this to debug native shenanigans that might happen
//#define DEBUG_POINT_TRACKER

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Jobs;

/// <summary>
/// From a sequence of Down, Move, and Up touch events (no Stationary)
/// this class can remember how many touch is HOLDING DOWN right now.
/// 
/// Plus it can keep an arbitrary boolean state for each holding down touch.
/// 
/// It currently cannot assign an ID to these touches. Just how many and where they are.
/// </summary>
public struct PointTracker : System.IDisposable
{
    private NativeList<float2> registeredPoints;
    private NativeHashMap<float2, Bool> registeredStates;
    private NativeHashMap<float2, int> registeredTouchId;
    private NativeArray<int> touchIdRunnerMemory;
    private int touchIdRunner
    {
        get => touchIdRunnerMemory[0];
        set => touchIdRunnerMemory[0] = value;
    }

    /// <summary>
    /// Make sure even if player use all of fingers and toes he still could not crash the game...
    /// </summary>
    public const int maximumTouch = 21;

    public void Dispose()
    {
        registeredPoints.Dispose();
        registeredStates.Dispose();
        registeredTouchId.Dispose();
        touchIdRunnerMemory.Dispose();
    }

    public PointTracker(Allocator allocator)
    {
        registeredPoints = new NativeList<float2>(allocator);
        registeredStates = new NativeHashMap<float2, Bool>(maximumTouch, allocator);
        registeredTouchId = new NativeHashMap<float2, int>(maximumTouch, allocator);
        touchIdRunnerMemory = new NativeArray<int>(1, allocator);
        touchIdRunner = 0;
    }

    /// <summary>
    /// All points in this list are currently "down". Not array for performance reason so don't modify the list! Just read it!
    /// </summary>
	public IEnumerable<float2> CurrentPoints
    {
        get
        {
            for (int i = 0; i < registeredPoints.Length; i++)
            {
                yield return registeredPoints[i];
            }
        }
    }

    /// <summary>
    /// You can keep whatever state you want with a bool per point.
    /// </summary>
    public Bool StateOfPoint(float2 point)
    {
        point = RoundVector(point);
        Bool ret;
        if (registeredStates.TryGetValue(point, out ret))
        {
            //DebugLog($"State of {point.x} {point.y} is {ret}", LogType.Log);
            return ret;
        }
        else
        {
            //DebugLog($"State of {point.x} {point.y} not found", LogType.Log);
            return false;
        }
    }

    /// <summary>
    /// Each touch gets a unique generated ID that is carried over from point to point.
    /// So you know a new touch in other frame is perhaps the same ones from earlier frame.
    /// </summary>
    public int IdOfPoint(float2 point)
    {
        point = RoundVector(point);
        int ret;
        if (registeredTouchId.TryGetValue(point, out ret))
        {
            //DebugLog($"Id of {point.x} {point.y} is {ret}", LogType.Log);
            return ret;
        }
        else
        {
            //DebugLog($"Id of {point.x} {point.y} not found", LogType.Log);
            return -1;
        }
    }

    public void Reset()
    {
        registeredPoints.Clear();
        registeredStates.Clear();
        registeredTouchId.Clear();
    }

    public void Down(float2 pointDown)
    {
        pointDown = RoundVector(pointDown);
        DebugLog($"Down {pointDown.x} {pointDown.y} ID : {touchIdRunner}", LogType.Log);
        registeredPoints.Add(pointDown);
        registeredStates.TryAdd(pointDown, false);
        registeredTouchId.TryAdd(pointDown, touchIdRunner);
        touchIdRunner = touchIdRunner + 1;
    }

    public void SetState(float2 pointNow, bool toState)
    {
        pointNow = RoundVector(pointNow);
        if (registeredPoints.Contains(pointNow) && registeredStates.TryGetValue(pointNow, out _))
        {
            //Debug.Log($"Set state OK {point.x} {point.y} {toState}");
            registeredStates.Remove(pointNow);
            registeredStates.TryAdd(pointNow, toState);
        }
#if DEBUG_POINT_TRACKER
        else
        {
            DebugLog($"Set state fail {pointNow.x} {pointNow.y} {toState}", LogType.Log);
        }
#endif
    }

    /// <summary>
    /// This is just to fight with Unity's floating point weirdness
    /// </summary>
    private float2 RoundVector(float2 vector) => new float2(math.round(vector.x), math.round(vector.y));

    public bool Move(float2 pointNow, float2 pointPrevious)
    {
        pointNow = RoundVector(pointNow);
        pointPrevious = RoundVector(pointPrevious);

        DebugLog($"Move {pointNow.x} {pointNow.y} {pointPrevious.x} {pointPrevious.y}", LogType.Log);

#if UNITY_IOS
        if (pointNow == pointPrevious)
        {
            //This weird bug iOS reports happen after an errornous Up.. we interpret this as Down.

            DebugLog($"Error Move!! {pointNow.x} {pointNow.y} {pointPrevious.x} {pointPrevious.y}", LogType.Error);
            Down(pointNow);
            return true;
        }
#endif

        Bool state;
        int touchId;

        Bool containsPrevious = registeredPoints.Contains(pointPrevious);

        if (containsPrevious)
        {
            float2 victim = pointPrevious;
            if (registeredStates.TryGetValue(victim, out state) && registeredTouchId.TryGetValue(victim, out touchId))
            {
                registeredPoints.RemoveAtSwapBack(registeredPoints.IndexOf(victim));
                registeredStates.Remove(victim);
                registeredTouchId.Remove(victim);
                registeredPoints.Add(pointNow);
                if (!registeredStates.TryGetValue(pointNow, out _) && !registeredTouchId.TryGetValue(pointNow, out _)) //somehow ArgumentException crash happen below!!
                {
                    registeredStates.TryAdd(pointNow, state); //copy state
                    registeredTouchId.TryAdd(pointNow, touchId); //copy touch ID too
                }
                return true;
            }
        }


        DebugLog($"No such previous point! (move) {pointPrevious.x} x {pointPrevious.y}", LogType.Error);
        return false;
    }

    public bool Up(float2 pointUp, float2 pointPrevious)
    {
        pointUp = RoundVector(pointUp);
        pointPrevious = RoundVector(pointPrevious);

        DebugLog($"Up {pointUp.x} {pointUp.y} {pointPrevious.x} {pointPrevious.y}", LogType.Log);

        //It has the same problem as Down
        bool containsPrevious = registeredPoints.Contains(pointPrevious);
        bool containsUp = false;

        if (!containsPrevious)
        {
            containsUp = registeredPoints.Contains(pointUp);
        }

        if (containsPrevious || containsUp )
        {
            float2 victim = containsUp ? pointUp : pointPrevious;
            if (registeredStates.TryGetValue(victim, out _) && registeredTouchId.TryGetValue(victim, out _))
            {
                registeredPoints.RemoveAtSwapBack(registeredPoints.IndexOf(victim));
                registeredStates.Remove(victim);
                registeredTouchId.Remove(victim);
                return true;
            }
        }

        DebugLog($"No such previous point! (up) {pointPrevious.x} x {pointPrevious.y}", LogType.Error);
        return false;
    }

    private void DebugLog(string message, LogType logType)
    {
#if DEBUG_POINT_TRACKER
        switch (logType)
        {
            case LogType.Log: { Debug.Log(message); return; }
            case LogType.Warning: { Debug.LogWarning(message); return; }
            case LogType.Error: { Debug.LogError(message); return; }
            default: { Debug.Log(message); return; }
        }
#endif
    }

}
