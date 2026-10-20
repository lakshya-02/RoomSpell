using System;
using System.Linq;
using System.Collections;
using System.Threading;
using Meta.XR;
using Meta.XR.BuildingBlocks.AIBlocks;
using UnityEngine;
using UnityEngine.Android;

namespace RoomSpell
{
    public sealed class RoomSpellDetection : MonoBehaviour
    {
        public PassthroughCameraAccess CameraAccess;
        public UnityInferenceEngineProvider Provider;
        public event Action<ObjectObservation[]> CandidatesChanged;
        public event Action<string> StatusChanged;
        public bool IsScanning { get; private set; }
        public bool IsBusy => busy;
        public ObjectObservation[] LastCandidates { get; private set; } = Array.Empty<ObjectObservation>();
        public float WarmupMilliseconds { get; private set; }
        public float LastInferenceMilliseconds { get; private set; }
        public int InferenceCount { get; private set; }
        public string LastStatus { get; private set; }
        public event Action<ObjectObservation[]> InferenceCompleted;
        readonly DetectionCandidates candidates = new DetectionCandidates();
        CancellationTokenSource lifetime;
        bool busy;
        bool warmed;
        bool startRequested;
        float nextInference;
        float cameraDeadline;
        Coroutine dummy;

        void Awake() => lifetime = new CancellationTokenSource();

        public async void StartScanning()
        {
            if (IsScanning) return;
            if (busy)
            {
                startRequested = true;
                Report("Finishing the previous scan, then restarting...");
                return;
            }
            if (!CameraAccess || !Provider)
            {
                Report("Camera or model configuration is missing.");
                return;
            }
            if (!PassthroughCameraAccess.IsSupported)
            {
                Report("Camera scanning needs a supported Quest 3/3S. Dummy mode works without a headset.");
                return;
            }
            IsScanning = true;
            cameraDeadline = Time.unscaledTime + 30;
            Report("Allow camera access, then look at three related objects.");
            if (!Permission.HasUserAuthorizedPermission(OVRPermissionsRequester.PassthroughCameraAccessPermission))
                Permission.RequestUserPermission(OVRPermissionsRequester.PassthroughCameraAccessPermission);
            CameraAccess.enabled = true;
            if (warmed) return;
            busy = true;
            try
            {
                Report("Preparing the on-device detection model...");
                await System.Threading.Tasks.Task.Yield();
                var started = Time.realtimeSinceStartup;
                await Provider.WarmUp();
                if (!this) return;
                WarmupMilliseconds = (Time.realtimeSinceStartup - started) * 1000;
                warmed = true;
            }
            catch (Exception ex)
            {
                StopScanning();
                Debug.LogException(ex);
                Report("Model could not start. Check the device log and retry.");
            }
            finally { FinishInference(); }
        }

        void Update()
        {
            if (!IsScanning || busy) return;
            if (!CameraAccess.IsPlaying)
            {
                if (Time.unscaledTime > cameraDeadline)
                {
                    StopScanning();
                    Report(Application.isEditor
                        ? "Simulator camera timed out: check the synthetic environment server and camera support."
                        : "Camera did not start. Allow the camera permission in app settings, then retry.");
                }
                return;
            }
            if (Time.unscaledTime < nextInference || !CameraAccess.IsUpdatedThisFrame) return;
            nextInference = Time.unscaledTime + 0.5f;
            Infer();
        }

        async void Infer()
        {
            busy = true;
            try
            {
                var started = Time.realtimeSinceStartup;
                var predictions = await Provider.DetectAsync(CameraAccess.GetTexture(), lifetime.Token);
                if (!this || !IsScanning || lifetime.IsCancellationRequested) return;
                LastInferenceMilliseconds = (Time.realtimeSinceStartup - started) * 1000;
                InferenceCount++;
                var observed = predictions.Select(x => new ObjectObservation(x.label, x.score)).ToArray();
                InferenceCompleted?.Invoke(observed);
                Publish(observed);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                StopScanning();
                Debug.LogException(ex);
                Report("Detection failed. Tap Scan room to retry.");
            }
            finally { FinishInference(); }
        }

        void FinishInference()
        {
            busy = false;
            if (!this) return;
            if (!IsScanning && CameraAccess) CameraAccess.enabled = false;
            if (!startRequested || !this || lifetime.IsCancellationRequested) return;
            startRequested = false;
            StartScanning();
        }

        public void StopScanning()
        {
            IsScanning = false;
            startRequested = false;
            candidates.Update(Array.Empty<ObjectObservation>());
            LastCandidates = Array.Empty<ObjectObservation>();
            if (dummy != null) { StopCoroutine(dummy); dummy = null; }
            // The provider reads texture dimensions after awaiting GPU work; keep its input alive until that work finishes.
            if (CameraAccess && !busy) CameraAccess.enabled = false;
        }

        void Publish(ObjectObservation[] observations)
        {
            LastCandidates = candidates.Update(observations);
            CandidatesChanged?.Invoke(LastCandidates);
        }

        void Report(string message)
        {
            LastStatus = message;
            StatusChanged?.Invoke(message);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public void StartDummy(int scenario)
        {
            StopScanning();
            var labels = DummyScenarios.Labels(scenario);
            dummy = StartCoroutine(DummyFrames(labels.Select(x => new ObjectObservation(x, 0.95f)).ToArray()));
        }

        IEnumerator DummyFrames(ObjectObservation[] observations)
        {
            Publish(observations);
            yield return new WaitForSecondsRealtime(0.5f);
            Publish(observations);
            dummy = null;
        }
#endif

        void OnApplicationPause(bool paused)
        {
            if (!paused || !IsScanning) return;
            StopScanning();
            Report("Scanning paused. Tap Scan room to resume.");
        }

        void OnDestroy()
        {
            lifetime?.Cancel();
            lifetime?.Dispose();
        }
    }
}
