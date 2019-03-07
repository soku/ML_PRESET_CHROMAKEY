using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using OpenCvSharp;
using UnityEngine.UI;
using System;
using System.IO;

public class Chromakey : MonoBehaviour {

	// path for background images
	public string BackGroundPath;

	// path for save combined image
	public string OutputPath = "";

	// object id for detection
	public int objectId;

	// save file index
	public int saveIndex;

	// region for webcam input
	public int captureX;
	public int captureY;
	public int captureWidth;
	public int captureHeight;

	// preview for all webcam input and region
	public RawImage preview;

	// background texture
	Texture2D _bgTexture;

	// combined texture and preview texture
	Texture2D _texture, _camTex;

	// webcap input manager
	WebCamManager webCamManager;

	// position of background image for write input
	int _destX, _destY;

	// background file index, random of 0 to background file count - 1
	int _bgIndex;

	// background files information of BackgroundPath
	FileInfo[] _bgFiles;

	// Use this for initialization
	void Start()
	{
		// initialize webcam texture to mat helper component
		webCamManager = GetComponent<WebCamManager>();
		webCamManager.Initialize();

		// get background files information
		DirectoryInfo backgrounds = new DirectoryInfo(BackGroundPath);
		_bgFiles = backgrounds.GetFiles();

		// load random index background image
		setBackground(UnityEngine.Random.Range(0, _bgFiles.Length));
	}

	// Update is called once per frame
	void Update()
	{
		// if webcam is playing
		if (webCamManager.IsPlaying() && webCamManager.DidUpdateThisFrame())
		{
			// get wabcam's mat
			Mat rgbaMat = webCamManager.GetMat();

			// make pixel buffer and copy input pixels
			// total : pixel count for input
			long total = rgbaMat.Total();
			Vec3b[] srcPixels = new Vec3b[total];
			//rgbaMat.GetArray(0, 0, srcPixels);

			// get pixels for output from background
			Color32[] dstPixels = _bgTexture.GetPixels32();

			// copy input pixel to output pixels buffer when pixel is not green
			// srcIndex : pixel index for input
			// dstIndex : pixel index for output
			long srcIndex = (captureY + captureHeight) * rgbaMat.Width + captureX;
			long dstIndex = _destY * _bgTexture.width + _destX;
			for (int y = 0; y < captureHeight; y++)
			{
				long si = srcIndex;
				long di = dstIndex;
				for (int x = 0; x < captureWidth; x++)
				{
					// if input pixel is not green (you need different values that matches background wall and light)
					if (srcPixels[si].Item1 < 50 || srcPixels[si].Item1 < srcPixels[si].Item2 + 20 || srcPixels[si].Item1 < srcPixels[si].Item0 + 20)
					{
						dstPixels[di].r = srcPixels[si].Item2;
						dstPixels[di].g = srcPixels[si].Item1;
						dstPixels[di].b = srcPixels[si].Item0;
					}
					// next pixel
					si++;
					di++;
				}
				// calculate for next line
				srcIndex -= rgbaMat.Width;
				dstIndex += _bgTexture.width;
			}
			// set combined pixels to output texture
			_texture.SetPixels32(dstPixels);
			_texture.Apply();

			// draw input rectangle for preview texture
			Cv2.Rectangle(rgbaMat, new OpenCvSharp.Rect(captureX, captureY, captureWidth, captureHeight), new Scalar(0, 255, 0, 255), 4);
			OpenCvSharp.Unity.MatToTexture(rgbaMat, _camTex);
		}
	}

	/// <summary>
	/// Raises the destroy event.
	/// </summary>
	void OnDestroy()
	{
		webCamManager.Dispose();
	}

	// load background image
	void setBackground(int index)
	{
		_bgIndex = index;

		// image file to texture
		byte[] fileData = File.ReadAllBytes(_bgFiles[_bgIndex].FullName);
		_bgTexture = new Texture2D(2, 2, TextureFormat.ARGB32, false);
		_bgTexture.LoadImage(fileData);
		_bgTexture.Apply();

		// make texture for output
		_texture = new Texture2D(_bgTexture.width, _bgTexture.height, TextureFormat.RGB24, false);
		GetComponent<RawImage>().texture = _texture;

		// fit camera for background image ratio
		float widthScale = Screen.width / (float)_bgTexture.width;
		float heightScale = Screen.height / (float)_bgTexture.height;
		if (widthScale < heightScale)
			Camera.main.orthographicSize = (_bgTexture.width * (float)Screen.height / (float)Screen.width) / 2;
		else
			Camera.main.orthographicSize = _bgTexture.height / 2;

		// set background position for write input to center of image
		_destX = (_bgTexture.width - captureWidth) / 2;
		_destY = (_bgTexture.height - captureHeight) / 2;
	}

	/// <summary>
	/// Raises the webcam texture to mat helper initialized event.
	/// </summary>
	public void OnWebCamTextureToMatHelperInitialized()
	{
		// make preview texture
		Mat rgbaMat = webCamManager.GetMat();
		_camTex = new Texture2D(rgbaMat.Width, rgbaMat.Height, TextureFormat.RGBA32, false);
		preview.texture = _camTex;
	}

	/// <summary>
	/// Raises the webcam texture to mat helper disposed event.
	/// </summary>
	public void OnWebCamTextureToMatHelperDisposed()
	{
	}

	/// <summary>
	/// Raises the webcam texture to mat helper error occurred event.
	/// </summary>
	/// <param name="errorCode">Error code.</param>
	public void OnWebCamTextureToMatHelperErrorOccurred(WebCamManager.ErrorCode errorCode)
	{
		Debug.Log("OnWebCamTextureToMatHelperErrorOccurred " + errorCode);
	}

	// called from ui when clicked change background button
	public void OnChange()
	{
		// reload random index background
		setBackground(UnityEngine.Random.Range(0, _bgFiles.Length));
	}

	// called from ui when clicked make output button
	public void OnMake()
	{
		// check output directory exist
		string path = OutputPath;
		if (!Directory.Exists(path))
			Directory.CreateDirectory(path);

		// save image file and meta text file for object detection
		File.WriteAllBytes(Path.Combine(path, "IMG_" + saveIndex + ".jpg"), _texture.EncodeToJPG());
		File.WriteAllText(Path.Combine(path, "IMG_" + saveIndex + ".txt"), string.Format("{0} {1} {2} {3} {4}", objectId, _destX, _destY, captureWidth, captureHeight));

		// increase save index and reload random index background image
		saveIndex++;
		setBackground(UnityEngine.Random.Range(0, _bgFiles.Length));
	}
}
