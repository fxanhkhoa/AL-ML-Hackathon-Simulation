using SocketIOClient;
using System;
using System.Collections;
using System.Collections.Generic;
using Unity.WebRTC;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Script.WebRTC
{
    public class CameraWebRTCSocketIO : MonoBehaviour
    {
        [SerializeField] private Camera streamingCamera;
        [SerializeField] private string signalServerUrl = "http://localhost:3000";
        [SerializeField] private RawImage displayImage;
        [SerializeField] private int imageWidth = 640;
        [SerializeField] private int imageHeight = 480;
        [SerializeField] private int quality = 75;
        [SerializeField]
        private float sendInterval = 1f;

        private RTCPeerConnection peerConnection;
        private MediaStream mediaStream;
        private SocketIOUnity socket;
        private RTCDataChannel dataChannel;
        private VideoStreamTrack videoStreamTrack;

        private Texture2D texture;
        private bool isConnected = false;

        // Use this for initialization
        void Start()
        {
            texture = new Texture2D(imageWidth, imageHeight, TextureFormat.RGB24, false);
            StartCoroutine(WebRTCInitialize());
        }

        // Update is called once per frame
        void Update()
        {
            if (videoStreamTrack != null && streamingCamera != null)
            {
                streamingCamera.Render();
            }
        }

        private IEnumerator SendImagesRoutine()
        {
            while (true)
            {
                Debug.Log("SEND IMAGE");
                if (isConnected)
                {
                    Debug.Log("SEND IMAGE 1");
                    SendCameraImage();
                }
                yield return new WaitForSeconds(sendInterval);
            }
        }

        private void SendCameraImage()
        {
            // Create a render texture
            RenderTexture rt = new RenderTexture(imageWidth, imageHeight, 24);

            // Set the camera's target texture and render
            streamingCamera.targetTexture = rt;
            streamingCamera.Render();

            // Read pixels from the render texture
            RenderTexture.active = rt;
            texture.ReadPixels(new Rect(0, 0, imageWidth, imageHeight), 0, 0);
            texture.Apply();

            // Reset camera and render texture
            streamingCamera.targetTexture = null;
            RenderTexture.active = null;
            Destroy(rt);

            // Convert to JPG and send via Socket.IO
            byte[] bytes = texture.EncodeToJPG(quality);
            string base64Image = Convert.ToBase64String(bytes);

            // Send the image through Socket.IO
            socket.Emit("broadcast_image", base64Image);
        }

        IEnumerator WebRTCInitialize()
        {
            // Connect to signaling server
            ConnectToSignalingServer();

            StartCoroutine(SendImagesRoutine());

            // Create and configure peer connection
            //CreatePeerConnection();

            // Setup streaming camera
            //SetupCameraStream();

            yield return null;
        }


        private void ConnectToSignalingServer()
        {
            // Setup Socket.IO connection
            socket = new SocketIOUnity(new Uri(signalServerUrl), new SocketIOOptions
            {
                Query = new Dictionary<string, string>
                {
                    {"token", "UNITY" }
                }
            ,
                EIO = EngineIO.V4
            ,
                Transport = SocketIOClient.Transport.TransportProtocol.WebSocket
            });

            // Socket.IO events
            socket.OnConnected += (sender, e) =>
            {
                Debug.Log("Connected to signaling server");
                socket.Emit("broadcaster");
                isConnected = true;
            };

            socket.OnUnityThread("watcher", response =>
            {
                Debug.Log("New watcher connected");
                StartCoroutine(CreateOffer());
            });

            socket.OnUnityThread("answer", response =>
            {
                try
                {
                    // Try parsing the response data
                    string sdp = response.GetValue<string>();
                    // Remove any quotes if they exist
                    StartCoroutine(SetRemoteDescription("answer", sdp));
                }
                catch (Exception e)
                {
                    Debug.LogError($"Error parsing answer SDP: {e.Message}\nResponse: {response}");
                }
            });

            socket.OnUnityThread("candidate", response =>
            {
                try
                {
                    // Log the raw response to help debug
                    Debug.Log($"Received candidate: {response}");

                    var str = response.GetValue<string>();
                    var candidate = JsonUtility.FromJson<RTCIceCandidateInit>(str);

                    RTCIceCandidate iceCandidate = new RTCIceCandidate(candidate);
                    peerConnection.AddIceCandidate(iceCandidate);
                }
                catch (Exception e)
                {
                    Debug.LogError($"Error parsing ICE candidate: {e.Message}");
                }
            });

            socket.Connect();
        }

        private void CreatePeerConnection()
        {
            // Configure ICE servers (STUN/TURN)
            RTCConfiguration config = new RTCConfiguration
            {
                iceServers = new[]
                {
                    new RTCIceServer { urls = new[] { "stun:stun.l.google.com:19302" } },
                    new RTCIceServer { urls = new[] { "stun:stun1.l.google.com:19302" } },
                    new RTCIceServer { urls = new[] { "stun:stun2.l.google.com:19302" } }
                }
            };

            // Create peer connection
            peerConnection = new RTCPeerConnection(ref config);

            // Set up event handlers
            peerConnection.OnIceCandidate = candidate =>
            {
                // Debug the candidate
                Debug.Log($"OnIceCandidate fired with: {candidate}");
                Debug.Log($"Candidate: {candidate.Candidate}");
                Debug.Log($"SdpMid: {candidate.SdpMid}");
                Debug.Log($"SdpMLineIndex: {candidate.SdpMLineIndex}");

                // Check if candidate is null or empty
                if (candidate == null || string.IsNullOrEmpty(candidate.Candidate))
                {
                    Debug.Log("Empty candidate received - this is normal at the end of ICE gathering");
                    return;
                }

                var str = JsonUtility.ToJson(candidate);
                Debug.Log($"str : {str}");
                socket.Emit("candidate", candidate);
            };

            peerConnection.OnIceConnectionChange = state =>
            {
                Debug.Log($"ICE connection state changed to: {state}");
            };

            peerConnection.OnConnectionStateChange = state =>
            {
                Debug.Log($"Connection state changed to: {state}");
            };
        }

        private void SetupCameraStream()
        {
            mediaStream = streamingCamera.CaptureStream(1280, 720);

            // Create video track from camera
            videoStreamTrack = streamingCamera.CaptureStreamTrack(1280, 720, RenderTextureDepth.Depth24);

            // Add track to peer connection
            foreach (var track in mediaStream.GetTracks())
            {
                Debug.Log($"Adding track: {track.Kind}, Enabled: {track.Enabled}, ID: {track.Id}");
                peerConnection.AddTrack(track, mediaStream);
            }
            //peerConnection.AddTrack(videoStreamTrack);

            displayImage.texture = streamingCamera.targetTexture;

            //if (!streamingCamera.enabled)
            //{
            //    Debug.LogError("Streaming camera is disabled!");
            //    streamingCamera.enabled = true;
            //}

            //// Ensure the camera has something to render (not looking at a black area)
            //Debug.Log($"Camera clear flags: {streamingCamera.clearFlags}");
            //Debug.Log($"Camera background color: {streamingCamera.backgroundColor}");
            //Debug.Log($"Camera culling mask: {streamingCamera.cullingMask}");

            //// Create video track from camera with explicit render texture
            //RenderTexture renderTexture = new RenderTexture(1280, 720, 24);
            //streamingCamera.targetTexture = renderTexture;

            //// Log to confirm render texture is created
            //Debug.Log($"Created render texture: {renderTexture.width}x{renderTexture.height}");

            //// Create video track from camera with the render texture
            //videoStreamTrack = streamingCamera.CaptureStreamTrack(1280, 720, RenderTextureDepth.Depth16);

            //// Add track to peer connection
            //peerConnection.AddTrack(videoStreamTrack);

            //// Log to confirm track is created
            //Debug.Log($"Created video track: {videoStreamTrack != null}");
        }

        IEnumerator CreateOffer()
        {
            RTCSessionDescriptionAsyncOperation op = peerConnection.CreateOffer();
            yield return op;

            if (op.IsError)
            {
                Debug.LogError($"Error creating offer: {op.Error.message}");
                yield break;
            }

            RTCSessionDescription desc = op.Desc;
            RTCSetSessionDescriptionAsyncOperation opSetDesc = peerConnection.SetLocalDescription(ref desc);
            yield return opSetDesc;

            if (opSetDesc.IsError)
            {
                Debug.LogError($"Error setting local description: {opSetDesc.Error.message}");
                yield break;
            }

            // Send offer to signaling server
            socket.Emit("offer", desc.sdp);
        }

        IEnumerator SetRemoteDescription(string type, string sdp)
        {
            RTCSessionDescription desc = new RTCSessionDescription
            {
                type = type == "offer" ? RTCSdpType.Offer : RTCSdpType.Answer,
                sdp = sdp
            };

            RTCSetSessionDescriptionAsyncOperation op = peerConnection.SetRemoteDescription(ref desc);
            yield return op;

            if (op.IsError)
            {
                Debug.LogError($"Error setting remote description: {op.Error.message}");
            }
        }

        void OnDestroy()
        {
            // Clean up resources
            if (videoStreamTrack != null)
                videoStreamTrack.Dispose();

            if (peerConnection != null)
                peerConnection.Close();

            if (socket != null)
                socket.Disconnect();
        }
    }
}