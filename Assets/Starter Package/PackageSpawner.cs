/*
 * Copyright 2021 Google LLC
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *      http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using TMPro;
using System.Net;
using System.Net.Sockets;
using System.Text;

public class PackageSpawner : MonoBehaviour
{
    public DrivingSurfaceManager DrivingSurfaceManager;
    public PackageBehaviour Package;
    public GameObject PackagePrefab;
    public GameObject MandarinPrefab;
    public GameObject HatPrefab;
    public int consumedPresentCount = -1;
    public int presentsNeededForKikiToFuckOff = 5;
    public TMP_Text giftCountTMP;

    public int port = 12345; // The port to broadcast to
    public string broadcastMessage = "Gifts Collected!";
    public float broadcastInterval = 0.9f; // Interval between broadcasts in seconds

    private bool kikiSatisfied = false;

    private UdpClient udpClient;
    private IPEndPoint broadcastEndPoint;

    [Header("UI Slide-In Settings")]
    public GameObject slidingUI;
    public Vector3 offScreenPosition;
    public Vector3 onScreenPosition;
    public float slideSpeed = 5f;
    public TMP_Text slidingUIText;
    public float textRevealSpeed = 0.05f;
    public float messageDisplayDuration = 3f;

    private Queue<string> messageQueue = new Queue<string>();
    private bool isMessageActive = false;

    private bool firstMessageShown = false;


    void Start()
    {
        udpClient = new UdpClient(); // Create UDP client
        udpClient.EnableBroadcast = true; // Enable broadcast mode

        // Set up the broadcast endpoint
        broadcastEndPoint = new IPEndPoint(IPAddress.Broadcast, port);

        // Initialize sliding UI position
        if (slidingUI != null)
        {
            slidingUI.transform.position = offScreenPosition;
        }
    }


    public static Vector3 RandomInTriangle(Vector3 v1, Vector3 v2)
    {
        float u = Random.Range(0.0f, 1.0f);
        float v = Random.Range(0.0f, 1.0f);
        if (v + u > 1)
        {
            v = 1 - v;
            u = 1 - u;
        }

        return (v1 * u) + (v2 * v);
    }

    public static Vector3 FindRandomLocation(ARPlane plane)
    {
        // Select random triangle in Mesh
        var mesh = plane.GetComponent<ARPlaneMeshVisualizer>().mesh;
        var triangles = mesh.triangles;
        var triangle = triangles[(int)Random.Range(0, triangles.Length - 1)] / 3 * 3;
        var vertices = mesh.vertices;
        var randomInTriangle = RandomInTriangle(vertices[triangle], vertices[triangle + 1]);
        var randomPoint = plane.transform.TransformPoint(randomInTriangle);

        return randomPoint;
    }

    public void SpawnPackage(ARPlane plane)
    {
        var selectedPrefab = MandarinPrefab;

        if (consumedPresentCount % 2 == 1) {
            selectedPrefab = PackagePrefab;
        }
        var packageClone = GameObject.Instantiate(selectedPrefab);
        packageClone.transform.position = FindRandomLocation(plane);

        Package = packageClone.GetComponent<PackageBehaviour>();
    }

    private void Update()
    {
        var lockedPlane = DrivingSurfaceManager.LockedPlane;
        if (lockedPlane != null)
        {
            if (Package == null)
            {
                SpawnPackage(lockedPlane);
                consumedPresentCount += 1;
            }

            var packagePosition = Package.gameObject.transform.position;
            packagePosition.Set(packagePosition.x, lockedPlane.center.y, packagePosition.z);
        }

        if (consumedPresentCount >= 0)
        {
            giftCountTMP.text = $"Gifts Collected: {consumedPresentCount}";
        }

        if (!kikiSatisfied && consumedPresentCount >= presentsNeededForKikiToFuckOff)
        {
            kikiSatisfied = true;
            // Start broadcasting
            InvokeRepeating(nameof(SendBroadcast), 0f, broadcastInterval);
            EnqueueMessage("Thanks for playing with me!");

        }
        HandleMessageQueue();

        if (!firstMessageShown)
        {
            firstMessageShown = true;
            EnqueueMessage("Find a surface to play with me on!");
        }
    }

    void SendBroadcast()
    {
        byte[] messageBytes = Encoding.UTF8.GetBytes(broadcastMessage);

        try
        {
            udpClient.Send(messageBytes, messageBytes.Length, broadcastEndPoint);
            Debug.Log($"Broadcast sent: {broadcastMessage}");
        }
        catch (SocketException e)
        {
            Debug.LogError($"SocketException: {e.Message}");
        }
    }

    void OnDestroy()
    {
        // Cleanup
        udpClient.Close();
    }

    public void EnqueueMessage(string message)
    {
        messageQueue.Enqueue(message);
    }

    private void HandleMessageQueue()
    {
        if (!isMessageActive && messageQueue.Count > 0)
        {
            StartCoroutine(DisplayMessage(messageQueue.Dequeue()));
        }
    }

    private IEnumerator DisplayMessage(string message)
    {
        isMessageActive = true;

        // Slide in the UI
        float elapsedTime = 0f;
        while (Vector3.Distance(slidingUI.transform.position, onScreenPosition) > 0.01f)
        {
            slidingUI.transform.position = Vector3.Lerp(slidingUI.transform.position, onScreenPosition, elapsedTime * slideSpeed);
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        slidingUI.transform.position = onScreenPosition;

        // Gradually reveal text
        slidingUIText.text = "";
        foreach (char c in message)
        {
            slidingUIText.text += c;
            yield return new WaitForSeconds(textRevealSpeed);
        }

        // Wait for display duration
        yield return new WaitForSeconds(messageDisplayDuration);

        // Slide out the UI
        elapsedTime = 0f;
        while (Vector3.Distance(slidingUI.transform.position, offScreenPosition) > 0.01f)
        {
            slidingUI.transform.position = Vector3.Lerp(slidingUI.transform.position, offScreenPosition, elapsedTime * slideSpeed);
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        slidingUI.transform.position = offScreenPosition;

        // Reset text
        slidingUIText.text = "";

        isMessageActive = false;
    }
}
