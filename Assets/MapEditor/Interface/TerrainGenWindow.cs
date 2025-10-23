using RustMapEditor.Variables;
using UnityEngine;
using System.IO;

public class TerrainGenWindow
{
	
	private static TerrainGenWindow _instance;
    public static TerrainGenWindow Instance => _instance;
	public static TemplateWindow Window;
	
	float min, max, minBlendFactor, maxBlendFactor;
	string topography;
	
	public static TemplateWindow CreateWindow(){		
			_instance = new TerrainGenWindow();		

			TemplateWindow window = AppManager.Instance.CreateWindow(
				titleText: "this title is reset by [BeginWindow]",
				rect: new Rect(300, -150, 1075, 500),
				iconPath: Path.Combine("HarmonyMods", "fillIcon.png")   //for appdata folders append to SettingsManager.AppDataPath()
			);

			if (window != null)		{		
				window.Build(_instance);
				ModManager.SkinGameObject(window.gameObject);				
				Debug.Log("Window created and configured");
			}
			else	{
				Debug.LogError("Failed to create window.");
			}
			Window = window;
			return window;
	}
	
	Layers targetLayer = new Layers();
	
	public void SyncPreview(){	
		if(Height){
			min = minHeight / 1000f;
			max = maxHeight / 1000f;
			minBlendFactor = minBlend /50f;
			maxBlendFactor = maxBlend /50f;
			topography = "heights";
		}
		else if(Slope){
			min = minSlope;
			max = maxSlope;
			minBlendFactor = minBlend * 15f;
			maxBlendFactor = maxBlend * 15f;
			topography = "slopes";
		}
		else if(Curve){
			min = minCurve /1000f;
			max = maxCurve /1000f;
			minBlendFactor = minBlend/500f;
			maxBlendFactor = maxBlend/500f;
			topography = "curves";
		}
		GenerativeManager.PreviewPaintRange(min - minBlendFactor, min, max, max + maxBlendFactor, topography, strength, PaintBlendMap);
		Debug.Log("preview updating");
	}
	
	public void SetKeyword(){
		LayerKeyword=Keyword.ToString().ToLower();
		Window.SyncWindow();
		Debug.Log("setting keyword");
	}
	
	public void SyncLayer(){
		targetLayer = TerrainManager.CreateLayerFromKeyword(LayerKeyword);
		Debug.Log("setting layer");
	}
	
	public void ExcludeSlopesCurves(){
		Debug.Log("exclude method for slopes firing");
		if (Height){
		Slope=false;
		Curve=false;
		}
		Window.SyncWindow();
		SyncPreview();
	}
	
	public void ExcludeHeightsCurves(){
		Debug.Log("exclude method for height firing");
		if (Slope){
		Curve=false;
		Height=false;
		}
		Window.SyncWindow();
		SyncPreview();
	}
	
	public void ExcludeSlopesHeights(){
		Debug.Log("exclude method for curve firing");
		if (Curve){
			Slope=false;
			Height=false;
		}
		Window.SyncWindow();
		SyncPreview();
	}	
	
	[BeginWindow("Terrain Fill")]  //this assigns the window title
	
	[HorizontalGroup]
	[OnBind("ExcludeSlopesCurves")]
		public bool Height = true;
	[OnBind("ExcludeHeightsCurves")]
		public bool Slope = false;
	[OnBind("ExcludeSlopesHeights")]
		public bool Curve = false;
	[OnBind("SyncPreview")] 
		public bool PaintBlendMap = false;
	[EndHorizontalGroup]
	
	[HorizontalGroup]
	[OnBind("SyncLayer")]
		public string LayerKeyword;
	[OnBind("SetKeyword")]
		public TerrainKeywords Keyword;
	[EndHorizontalGroup]


	[RangeSlideMin(0f, 1000f, false, "Height")]	[OnBind("SyncPreview")]
		public float minHeight = 0f;  
	[RangeSlideMax]
		public float maxHeight = 500f;

	[RangeSlideMin(0f, 90f, false, "Slope")]	[OnBind("SyncPreview")]
		public float minSlope = 0f;  
	[RangeSlideMax]
		public float maxSlope = 20f;	
	
	[RangeSlideMin(-3f, 3f, false, "Curvature")]	[OnBind("SyncPreview")]
		public float minCurve = 0f;  
	[RangeSlideMax]
		public float maxCurve = .1f;
	
	[HorizontalGroup]	
	[OnBind("SyncPreview")]	[SliderUIElement(0f, 1f, false, "MinBlend")]
		public float minBlend = 0f;
	[OnBind("SyncPreview")]	[SliderUIElement(0f, 1f, false, "MaxBlend")]
		public float maxBlend = 0f;	
	[EndHorizontalGroup]
	
	[HorizontalGroup]	
	[OnBind("SyncPreview")] 	[SliderUIElement(0f, 1f, false, "Strength")]
		public float strength = 1f;
	[RustMapperButton("Apply Fill")]
		public void Apply(){		
			if(PaintBlendMap){	GenerativeManager.PaintBlend(TerrainManager.CreateLayerFromKeyword(LayerKeyword), min - minBlendFactor, min, max, max + maxBlendFactor, topography, strength); }
			else{ GenerativeManager.PaintRange(TerrainManager.CreateLayerFromKeyword(LayerKeyword), min - minBlendFactor, min, max, max + maxBlendFactor, topography, strength);}
		}
		[RustMapperButton("Copy Command")]
		public void CopyCommandToClipboard()
		{
			// Determine min/max and blend factors based on active topography mode
			float minVal, maxVal, minBlendFactor, maxBlendFactor;
			string topography;

			if (Height)
			{
				minVal = minHeight / 1000f;
				maxVal = maxHeight / 1000f;
				minBlendFactor = minBlend / 50f;
				maxBlendFactor = maxBlend / 50f;
				topography = "heights";
			}
			else if (Slope)
			{
				minVal = minSlope;
				maxVal = maxSlope;
				minBlendFactor = minBlend * 15f;
				maxBlendFactor = maxBlend * 15f;
				topography = "slopes";
			}
			else if (Curve)
			{
				minVal = minCurve / 1000f;
				maxVal = maxCurve / 1000f;
				minBlendFactor = minBlend / 500f;
				maxBlendFactor = maxBlend / 500f;
				topography = "curves";
			}
			else
			{
				// Fallback: should not happen due to mutual exclusion, but safe default
				minVal = maxVal = 0f;
				minBlendFactor = maxBlendFactor = 0f;
				topography = "heights";
			}

			// Calculate final range bounds including blend
			float rangeMinOuter = minVal - minBlendFactor;
			float rangeMinInner = minVal;
			float rangeMaxInner = maxVal;
			float rangeMaxOuter = maxVal + maxBlendFactor;

			// Get the string representation of the selected keyword
			string keywordStr = LayerKeyword?.ToLower() ?? "dirt";

			// Choose method: PaintBlend if checkbox is on, otherwise PaintRange
			string method = PaintBlendMap ? "PaintBlend" : "PaintRange";

			// Construct the final command string
			string command = $"g.{method} {keywordStr},{rangeMinOuter:F6},{rangeMinInner:F6},{rangeMaxInner:F6},{rangeMaxOuter:F6},{topography},{strength:F6}";

			// Copy to clipboard
			GUIUtility.systemCopyBuffer = command;

			// Optional: Log for debugging
			Debug.Log($"Terrain fill command copied to clipboard:\n{command}");
		}
	[EndHorizontalGroup]
	
	
	[OnWindowEnable]
	public void WindowEnabled(){
		TerrainManager.UpdateHeightCache();
		TerrainManager.ShowLandMask();
	}
	
	[OnWindowDisable]
	public void WindowDisabled(){
		TerrainManager.HideLandMask();
	}	
	
	public enum TerrainKeywords	{
		Dirt,
		Snow,
		Sand,
		Rock,
		Grass,
		Forest,
		Stones,
		Gravel,
		Arid,
		Temperate,
		Tundra,
		Arctic,
		Jungle,
		Field,
		Cliff,
		Summit,
		Beach,
		Beachside,
		Foresttopo,
		Ocean,
		Oceanside,
		Riverside,
		Lakeside,
		Road,
		Roadside,
		Railside,
		Swamp,
		River,
		Lake,
		Offshore,
		Rail,
		Building,
		Cliffside,
		Mountain,
		Clutter,
		Alt,
		Tier0,
		Tier1,
		Tier2,
		Mainland,
		Hilltop
	}

}