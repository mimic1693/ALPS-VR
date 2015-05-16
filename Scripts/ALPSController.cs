﻿/************************************************************************
	ALPSController is the main class which manages custom rendering

    Copyright (C) 2014  ALPS VR.

    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.

************************************************************************/

using UnityEngine;
using System.Collections;

[System.Serializable]
public class ALPSController : MonoBehaviour {

	//=====================================================================================================
	// Attributes
	//=====================================================================================================

	/**Public**/
	//The current device configuration
	public ALPSConfig deviceConfig = ALPSDevice.GetConfig(Device.DEFAULT);

	//One camera for each eye
	public GameObject cameraLeft;
	public GameObject cameraRight;

	//Head represents user's head
	public GameObject head;

	//Render textures
	public RenderTexture srcTex;
	public RenderTexture destTex;

	//Screen size
	public static int screenWidthPix;
	public static int screenHeightPix;

	//Material
	public Material mat;

	//Crosshairs
	public bool crosshairsEnabled;

	/**Private**/
	private Rect rectLeft,rectRight;
	private float DPI;

	//=====================================================================================================
	// Functions
	//=====================================================================================================

	/// <summary>
	/// Initializes side-by-side rendering and head tracking. 
	/// </summary>
	public void Awake(){
		ALPSCamera.deviceConfig = deviceConfig;
		ALPSBarrelMesh.deviceConfig = deviceConfig;
		ALPSCrosshairs.deviceConfig = deviceConfig;
		ALPSGUI.controller = this;

		head = new GameObject ("ALPSHead");
		head.transform.parent = transform;
		head.transform.position = transform.position;

		#if UNITY_EDITOR
			head.AddComponent("MouseLook");
			screenWidthPix = Screen.width;
			screenHeightPix = Screen.height;
		#elif UNITY_ANDROID
            System.Type GyroType = typeof(ALPSGyro);
			head.AddComponent(GyroType);
			Screen.orientation = ScreenOrientation.LandscapeLeft;
			ALPSAndroid.Init ();
			screenWidthPix = ALPSAndroid.WidthPixels ();
			screenHeightPix = ALPSAndroid.HeightPixels ();
#endif

        //Make sure the longer dimension is width as the phone is always in landscape mode
		if(screenWidthPix<screenHeightPix){
			int tmp = screenHeightPix;
			screenHeightPix = screenWidthPix;
			screenWidthPix = tmp;
		}

		for (var i=0; i<2; i++) {
			bool left = (i==0);
			GameObject OneCamera = new GameObject(left?"CameraLeft":"CameraRight");
			OneCamera.AddComponent("Camera");
			OneCamera.AddComponent("ALPSCamera");
			(OneCamera.GetComponent("ALPSCamera") as ALPSCamera).leftEye = left;
			OneCamera.transform.parent = head.transform;
			OneCamera.transform.position = head.transform.position;
			if(left)cameraLeft = OneCamera;
			else cameraRight = OneCamera;
		}

		ALPSCamera[] ALPSCameras = FindObjectsOfType(typeof(ALPSCamera)) as ALPSCamera[];
		foreach (ALPSCamera cam in ALPSCameras) {
			cam.Init();
		}

		mat = Resources.Load ("Materials/ALPSDistortion") as Material;

		DPI = Screen.dpi;

		//Render Textures
		srcTex = new RenderTexture (2048, 1024, 16);
		destTex = camera.targetTexture;
		cameraLeft.camera.targetTexture = cameraRight.camera.targetTexture = srcTex;

		// Setting the main camera
		camera.aspect = 1f;
		camera.backgroundColor = Color.black;
		camera.clearFlags =  CameraClearFlags.Nothing;
		camera.cullingMask = 0;
		camera.eventMask = 0;
		camera.orthographic = true;
		camera.renderingPath = RenderingPath.Forward;
		camera.useOcclusionCulling = false;
		cameraLeft.camera.depth = 0;
		cameraRight.camera.depth = 1;
		camera.depth = Mathf.Max (cameraLeft.camera.depth, cameraRight.camera.depth) + 1;

		cameraLeft.gameObject.AddComponent("ALPSCrosshairs");
		cameraRight.gameObject.AddComponent("ALPSCrosshairs");

		AudioListener[] listeners = FindObjectsOfType(typeof(AudioListener)) as AudioListener[];
		if (listeners.Length < 1) {
			gameObject.AddComponent ("AudioListener");
		}

		ClearDirty();
	}

	/// <summary>
	/// Renders scene for both cameras.
	/// </summary>
	public void OnPostRender(){
		RenderTexture.active = destTex;
		GL.Clear (false,true,Color.black);
		RenderEye (true,srcTex);
		RenderEye (false,srcTex);
		srcTex.DiscardContents ();
	}

	/// <summary>
	/// Renders scene for one camera.
	/// </summary>
	/// <param name="_leftEye">True if renders for the left camera, false otherwise.</param>
	/// <param name="_source">Source texture on which the camera renders.</param>
	private void RenderEye(bool _leftEye, RenderTexture _source){
		mat.mainTexture = _source;
		mat.SetVector("_SHIFT",new Vector2(_leftEye?0:0.5f,0));
		float convergeOffset = ((deviceConfig.Width * 0.5f) - deviceConfig.IPD) / deviceConfig.Width;
		mat.SetVector("_CONVERGE",new Vector2((_leftEye?1f:-1f)*convergeOffset,0));
		mat.SetFloat ("_AberrationOffset",deviceConfig.enableChromaticCorrection?deviceConfig.chromaticCorrection:0f);
		float ratio = (deviceConfig.IPD*0.5f) / deviceConfig.Width;
		mat.SetVector ("_Center",new Vector2(0.5f+(_leftEye?-ratio:ratio),0.5f));

		GL.Viewport (_leftEye ? rectLeft : rectRight);

		GL.PushMatrix ();
		GL.LoadOrtho ();
		mat.SetPass (0);
		if(_leftEye)cameraLeft.GetComponent<ALPSCamera>().Draw ();
		else cameraRight.GetComponent<ALPSCamera>().Draw ();
		GL.PopMatrix ();
	}

	/// <summary>
	/// Resets all the settings and applies the current DeviceConfig
	/// </summary>
	public void ClearDirty(){
		//We give the current DPI to the new ALPSConfig
		deviceConfig.DPI = DPI;
		if (deviceConfig.DPI <= 0) {
			deviceConfig.DPI = ALPSConfig.DEFAULT_DPI;
		}
	
		if(cameraLeft!=null && cameraRight!=null){
			float widthPix = deviceConfig.WidthPix();
			float heightPix = deviceConfig.HeightPix();

			rectLeft  = new Rect (screenWidthPix*0.5f-widthPix*0.5f,screenHeightPix*0.5f-heightPix*0.5f,widthPix*0.5f,heightPix);
			rectRight = new Rect (screenWidthPix*0.5f,screenHeightPix*0.5f-heightPix*0.5f,widthPix*0.5f,heightPix);

			Vector3 camLeftPos = cameraLeft.transform.localPosition; 
			camLeftPos.x = -deviceConfig.ILD*0.0005f;
			cameraLeft.transform.localPosition = camLeftPos;
			
			Vector3 camRightPos = cameraRight.transform.localPosition;
			camRightPos.x = deviceConfig.ILD*0.0005f;
			cameraRight.transform.localPosition = camRightPos;
			
			cameraLeft.camera.fieldOfView = deviceConfig.fieldOfView;
			cameraRight.camera.fieldOfView = deviceConfig.fieldOfView;

			cameraLeft.GetComponent<ALPSCamera>().UpdateMesh();
			cameraRight.GetComponent<ALPSCamera>().UpdateMesh();
		}

		ALPSCrosshairs[] ch = GetComponentsInChildren<ALPSCrosshairs> ();
		foreach (ALPSCrosshairs c in ch) {
			c.UpdateCrosshairs();
			c.enabled = crosshairsEnabled;
		}
	}

	/// <summary>
	/// Indicates whether viewport should be fullscreen or fixed in size
	/// </summary>
	/// <param name="_fixed">True if fixed in size, false if fullscreen.</param>
	public void SetFixedSize(bool _fixed){
		if (_fixed != deviceConfig.fixedSize) {
			ClearDirty();
		}
		deviceConfig.fixedSize = _fixed;
	}

	/// <summary>
	/// Sets a new device configuration.
	/// </summary>
	// <param name="_device">Name of the device.</param>
	public void SetDevice(Device _device){
		deviceConfig = ALPSDevice.GetConfig (_device);
		ALPSCamera.deviceConfig = deviceConfig;
		ALPSBarrelMesh.deviceConfig = deviceConfig;
		ALPSCrosshairs.deviceConfig = deviceConfig;
		ClearDirty ();
	}

	/// <summary>
	/// Copy camera settings to left and right cameras. Will overwrite culling masks.
	/// </summary>
	/// <param name="_cam">The camera from which you want to copy the settings.</param>
	public void SetCameraSettings(Camera _cam){
		cameraLeft.camera.CopyFrom (_cam);
		cameraRight.camera.CopyFrom (_cam);
		cameraLeft.camera.rect = new Rect (0,0,0.5f,1);
		cameraRight.camera.rect = new Rect (0.5f,0,0.5f,1);
	}
	
	/// <summary>
	/// Adds left and right layers to the existing culling masks for left and right cameras.
	/// </summary>
	/// <param name="_leftLayer">Name of the layer rendered by the left camera.</param>
	/// <param name="_rightLayer">Name of the layer rendered by the right camera.</param>
	public int SetStereoLayers(string _leftLayer, string _rightLayer){
		int leftLayer = LayerMask.NameToLayer (_leftLayer);
		int rightLayer = LayerMask.NameToLayer (_rightLayer);
		if (leftLayer < 0 && rightLayer < 0) return -1;
		
		cameraLeft.camera.cullingMask |= 1 << LayerMask.NameToLayer(_leftLayer);
		cameraLeft.camera.cullingMask &=  ~(1 << LayerMask.NameToLayer(_rightLayer));
		
		cameraRight.camera.cullingMask |= 1 << LayerMask.NameToLayer(_rightLayer);
		cameraRight.camera.cullingMask &=  ~(1 << LayerMask.NameToLayer(_leftLayer));
		
		return 0;
	}
	
	/// <summary>
	/// Returns point of view position. This can be useful for setting up a Raycast.
	/// </summary>
	public Vector3 PointOfView(){
		//returns current position plus NeckToEye vector
		return new Vector3(transform.position.x,transform.position.y + ALPSConfig.neckPivotToEye.y*0.001f,transform.position.z + ALPSConfig.neckPivotToEye.x*0.001f);
	}
	
	/// <summary>
	/// Returns forward direction vector. This can be useful for setting up a Raycast.
	/// </summary>
	public Vector3 ForwardDirection(){
		return cameraLeft.camera.transform.forward;
	}
	
	/// <summary>
	/// Returns left and right cameras.
	/// </summary>
	public Camera[] GetCameras(){
		Camera[] cams = {cameraLeft.camera, cameraRight.camera};
		return cams;
	}
}