using System.Collections;
using System.Collections.Generic;
using Unity.WebRTC;
using UnityEngine;
using SocketIOClient;
using System.Net.Sockets;
using System;
using UnityEngine.UI;
using System.Threading.Tasks;
using System.Linq;

public class CameraWebRTC : MonoBehaviour
{
    [SerializeField] private Camera streamCamera;
    [SerializeField] private int width = 1280;
    [SerializeField] private int height = 720;
    [SerializeField] private int frameRate = 30;
    [SerializeField] private RawImage displayImage;

    private RTCPeerConnection peerConnection;
    private RTCDataChannel dataChannel;
    private RenderTexture cameraRenderTexture;
    private List<RTCRtpSender> rtpSenders = new List<RTCRtpSender>();

    private MediaStream videoStream;

    private SocketIOUnity socket;
    private bool isConnected = false;
    private HashSet<string> sentCandidates = new HashSet<string>();
    private List<RTCIceCandidate> pendingCandidates = new List<RTCIceCandidate>();
    private bool remoteDescriptionSet = false;


    private void Awake()
    {
    }

    // Use this for initialization
    void Start()
    {
        StartCoroutine(SetupSocketIO());
    }

    IEnumerator SetupSocketIO()
    {
        var uri = new Uri("http://localhost:3000");
        socket = new SocketIOUnity(uri, new SocketIOOptions
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

        // Set up event handlers
        socket.OnConnected += Socket_OnConnected;
        socket.OnDisconnected += Socket_OnDisconnected;

        // Set up a listener for a specific event
        socket.On("chat message", response =>
        {
            string message = response.GetValue<string>();
            Debug.Log($"Received message: {message}");

            // Note: Socket.IO callbacks run on a different thread
            // Use Unity's main thread if you need to update UI
        });

        socket.OnUnityThread("answer", response =>
        {
            var offer = response.GetValue<string>();
            try
            {
                //MainThreadDispatcher.Instance().Enqueue(() =>
                //{
                Debug.Log($"Received answer from server:");
                if (peerConnection != null)
                {
                    Debug.Log($"Processing answer on main thread: {offer}");
                    var fullSdp = JsonUtility.FromJson<RTCSessionDescription>(offer);
                    Debug.Log($"PARSED ANSWER: {fullSdp}");

                    StartCoroutine(SetRemoteDesc(fullSdp, peerConnection));
                }
                else
                {
                    Debug.LogError("PeerConnection is null when trying to set remote description");
                }
                //});
            }
            catch (Exception ex)
            {
                Debug.LogError(ex.Message);
            }
        });

        socket.OnUnityThread("candidate", response =>
        {
            try
            {
                var candidate = response.GetValue<string>();
                Debug.Log("Received candidate: " + candidate + candidate != null);

                if (candidate != null)
                {
                    Debug.Log("HERE 1");
                    var parsedCandidate = JsonUtility.FromJson<RTCIceCandidateInit>(candidate);
                    Debug.Log("HERE 2");
                    Debug.Log("HERE 3");

                    if (string.IsNullOrEmpty(parsedCandidate.candidate))
                    {
                        Debug.LogWarning("Received empty ICE candidate string, ignoring");
                        return;
                    }

                    // Ensure at least one of sdpMid or sdpMLineIndex is not null
                    if (parsedCandidate.sdpMid == null && parsedCandidate.sdpMLineIndex == null)
                    {
                        Debug.LogWarning("Both sdpMid and sdpMLineIndex are null, using defaults");
                        // Provide default values - typically the first media line
                        parsedCandidate.sdpMid = "0";
                        parsedCandidate.sdpMLineIndex = 0;
                    }
                    var iceCandidate = new RTCIceCandidate(parsedCandidate);

                    if (remoteDescriptionSet)
                    {
                        Debug.Log("HERE 4");
                        // If remote description is set, add the candidate immediately
                        var result = peerConnection.AddIceCandidate(iceCandidate);
                        Debug.Log($"Added ICE candidate immediately: {result}");
                    }
                    else
                    {
                        Debug.Log("HERE 5");
                        // Otherwise, store it to add later
                        pendingCandidates.Add(iceCandidate);
                        Debug.Log("Stored ICE candidate for later");
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error Add ice Candidate: {e.Message}");
            }
        });

        var op = socket.ConnectAsync();
        yield return op;
        var op1 = socket.EmitAsync("join", "AI");
        yield return op1;

        try
        {
            // Create render texture for the camera
            //cameraRenderTexture = new RenderTexture(width, height, 0, RenderTextureFormat.BGRA32);
            //cameraRenderTexture.Create();
            //streamCamera.targetTexture = cameraRenderTexture;


            videoStream = streamCamera.CaptureStream(width, height);
            //track = videoStream.GetTracks().First();
            displayImage.texture = streamCamera.targetTexture;
            Debug.Log("Media stream created");

            // Create media stream
            //videoStream = new MediaStream();
            //VideoStreamTrack videoTrack = new VideoStreamTrack(cameraRenderTexture);
            //videoStream.AddTrack(videoTrack);
            //Debug.Log("Video track added");

            Debug.Log($"Number of tracks in stream: {videoStream.GetTracks().Count()}");
            foreach (var track in videoStream.GetTracks())
            {
                Debug.Log($"Track kind: {track.Kind}, enabled: {track.Enabled}");
            }

            // Create peer connections
            var configuration = new RTCConfiguration
            {
                iceServers = new[] { new RTCIceServer { urls = new[] { "stun:stun.l.google.com:19302" } } }
            };

            peerConnection = new RTCPeerConnection(ref configuration);

            // Set up event handlers
            peerConnection.OnIceCandidate = candidate =>
            {
                Debug.Log($"OnIceCandidate fired: {candidate}");
                // Emit candidate Here
                if (candidate != null)
                {
                    string candidateStr = candidate.ToString();
                    if (!sentCandidates.Contains(candidateStr))
                    {
                        sentCandidates.Add(candidateStr);
                        Debug.Log("Emitting candidate: " + candidateStr);

                        SocketIO_SendObject("candidate", JsonUtility.ToJson(candidate));
                    }
                    else
                    {
                        //Debug.Log("Skipping duplicate candidate");
                    }
                }
            };

            peerConnection.OnNegotiationNeeded = () =>
            {
                Debug.Log("Negotiation needed event fired");
                StartCoroutine(CreateOffer());
            };

            peerConnection.OnTrack = e =>
            {
                Debug.Log($"OnTrack event fired. Kind: {e.Track.Kind}");
            };


            peerConnection.OnConnectionStateChange = state =>
            {
                try
                {

                    Debug.Log("Local connection state changed to: " + state);

                    switch (state)
                    {
                        case RTCPeerConnectionState.Connected:
                            Debug.Log("Connection established successfully!");
                            Debug.Log($"Number of active senders: {rtpSenders.Count}");
                            foreach (var sender in rtpSenders)
                            {
                                Debug.Log($"Sender track: {sender.Track?.Kind}, Active: {sender.Track?.Enabled}");
                            }
                            break;
                        case RTCPeerConnectionState.New:
                            Debug.Log("Connection is new");
                            break;
                        case RTCPeerConnectionState.Connecting:
                            Debug.Log("Connection is connecting...");
                            break;
                        case RTCPeerConnectionState.Disconnected:
                            Debug.Log("Connection disconnected");
                            break;
                        case RTCPeerConnectionState.Failed:
                            Debug.Log("Connection failed - check ICE servers and network");
                            break;
                        case RTCPeerConnectionState.Closed:
                            Debug.Log("Connection closed");
                            break;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            };

            peerConnection.OnIceConnectionChange = state =>
            {
                Debug.Log("ICE connection state: " + state);
            };

            peerConnection.OnIceGatheringStateChange = state =>
            {
                Debug.Log("ICE gathering state: " + state);
            };

            peerConnection.OnDataChannel = channel =>
            {
                Debug.Log("Data channel created");
                dataChannel = channel;
                dataChannel.OnMessage = message =>
                {
                    Debug.Log("Received message: " + message);
                };
            };

            Debug.Log("Peer connection created");
        }
        catch (Exception e)
        {
            Debug.LogError($"Connection error: {e.Message}");
        }
    }

    IEnumerator SetRemoteDesc(RTCSessionDescription sdp, RTCPeerConnection peer)
    {
        var op = peer.SetRemoteDescription(ref sdp);
        yield return op;

        if (op.IsError)
        {
            Debug.LogError($"Error setting remote description: {op.Error}");
            yield break;
        }

        Debug.Log("Set remote description successfully");
        remoteDescriptionSet = true;

        // Now add any pending ICE candidates
        Debug.Log($"Adding {pendingCandidates.Count} pending ICE candidates");
        foreach (var candidate in pendingCandidates)
        {
            var result = peerConnection.AddIceCandidate(candidate);
            Debug.Log($"Added pending ICE candidate: {result}");
        }
        pendingCandidates.Clear();
    }

    public void startStreamingClick()
    {
        Debug.Log("Start streaming");
        StartCoroutine(WebRTCCoroutine());
    }

    // Update is called once per frame
    void Update()
    {

    }

    private IEnumerator WebRTCCoroutine()
    {
        // Add tracks to local peer connection
        Debug.Log($"Adding {videoStream.GetTracks().Count()} tracks to peer connection");
        foreach (var track in videoStream.GetTracks())
        {
            Debug.Log($"Adding track: {track.Kind}, Enabled: {track.Enabled}, ID: {track.Id}");
            peerConnection.AddTrack(track, videoStream);
        }

        StartCoroutine(CreateOffer());

        yield return null;
    }

    private IEnumerator CreateOffer()
    {
        Debug.Log("Creating offer");

        if (peerConnection == null)
        {
            Debug.LogError("PeerConnection is null");
            yield break;
        }

        // Check if tracks have been added
        Debug.Log($"Number of senders: {rtpSenders.Count}");


        var offerOp = peerConnection.CreateOffer();
        yield return offerOp;

        if (offerOp.IsError)
        {
            Debug.LogError($"Error creating offer: {offerOp.Error}");
            yield break;
        }

        RTCSessionDescription offer = offerOp.Desc;

        var setLocalDescOp = peerConnection.SetLocalDescription(ref offer);
        yield return setLocalDescOp;

        if (setLocalDescOp.IsError)
        {
            Debug.LogError($"Error setting local description: {setLocalDescOp.Error}");
            yield break;
        }

        Debug.Log("Offer created: " + offer);
        var offerStr = JsonUtility.ToJson(offer);
        SocketIO_SendMessage("offer", offerStr);

        Debug.Log($"SDP contains video: {offer.sdp.Contains("m=video")}");
        Debug.Log($"SDP: {offer.sdp}");
    }

    async private void OnDestroy()
    {
        if (socket != null)
        {
            await socket.DisconnectAsync();
            socket.Dispose();
        }

        // Clean up
        foreach (var sender in rtpSenders)
        {
            peerConnection.RemoveTrack(sender);
        }

        if (videoStream != null)
        {
            foreach (var track in videoStream.GetTracks())
            {
                track.Dispose();
            }
            videoStream.Dispose();
        }

        if (peerConnection != null)
        {
            peerConnection.Close();
            peerConnection.Dispose();
        }

        if (cameraRenderTexture != null)
        {
            if (streamCamera != null)
                streamCamera.targetTexture = null;

            cameraRenderTexture.Release();
            Destroy(cameraRenderTexture);
        }

        // WebRTC.Dispose() is called automatically when exiting the application
    }
    private void Socket_OnConnected(object sender, EventArgs e)
    {
        isConnected = true;
        Debug.Log("Connected to server");

        // Send a message after connection
        SocketIO_SendMessage("welcome", "Hello from Unity!");
    }

    private void Socket_OnDisconnected(object sender, string e)
    {
        isConnected = false;
        Debug.Log($"Disconnected: {e}");
    }

    // Method to send a message to the server
    public async void SocketIO_SendMessage(string topic, string message, string roomID = "AI")
    {
        if (!isConnected) return;

        try
        {
            await socket.EmitAsync(topic, message, roomID);
            Debug.Log($"Message sent: {message}");
        }
        catch (Exception e)
        {
            Debug.LogError($"Error sending message: {e.Message}");
        }
    }

    public async void SocketIO_SendObject(string topic, object obj, string roomID = "AI")
    {
        if (!isConnected) return;

        try
        {
            await socket.EmitAsync(topic, obj, roomID);
            Debug.Log($"Message sent: {obj}");
        }
        catch (Exception e)
        {
            Debug.LogError($"Error sending message: {e.Message}");
        }
    }
}
