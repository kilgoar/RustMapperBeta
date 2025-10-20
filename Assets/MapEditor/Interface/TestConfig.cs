using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TestItem
{
    [RustMapperUIElement("Item Name")]
    public string Name = "Item";
    public float Value = 1.0f;
    public bool Enabled = true;
}

public enum TestMode { Easy, Medium, Hard }

public class TestConfig
{
	public void TestMessage(){
		Debug.Log("onbind is working correctly");
	}
	
	
    [BeginWindow("Test Configuration Window")]
    public readonly string Description = "This is a test configuration";

    [RustMapperUIElement("Custom Text")]
    public string EditableText = "Hello, UI!";

    public float Speed = 5.0f;

    [HorizontalGroup]
    public int Count = 100;

    public bool IsActive = true;

    [EndHorizontalGroup]
    public TestMode Mode = TestMode.Medium;

    public Vector3 Position = new Vector3(0, 1, 0);

    public List<TestItem> Items = new List<TestItem>
    {
        new TestItem { Name = "Item1", Value = 1.0f, Enabled = true },
        new TestItem { Name = "Item2", Value = 2.0f, Enabled = false }
    };

	[Sync]  //this tag causes a call to refresh all UI element values after button executes
    public void ResetValues()
    {
        Debug.Log("Resetting configuration values");
        EditableText = "Reset";
        Speed = 0.0f;
        Count = 0;
        IsActive = false;
        Mode = TestMode.Easy;
        Position = Vector3.zero;
        Items.Clear();
    }

	[Sync]
    [RustMapperButton("Apply Changes")]
    public void ApplyChanges()
    {
        Debug.Log("Applying configuration changes");
        Speed = 20;
    }

    [SliderUIElement(0f, 10f, true, "Volume Level")]
    public int Volume = 5;  // Bound to slider (whole numbers)

    [SliderUIElement(0.5f, 2f, false, "Speed Factor")]
	[OnBind("TestMessage")]
    public float SpeedFactor = 1f;  // Bound to slider (decimal)

    [RangeSlideMin(0.5f, 2f, false, "Speed Multiplier")]
	[OnBind("TestMessage")]
    public float SpeedLow = 1f;  // Bound to low slider

    [RangeSlideMax]
    public float SpeedHigh = 1.5f; // Bound to high slider (marker attribute)
	
	[OnWindowEnable]
	public void WindowEnable(){
		Debug.Log("test window enabled");
	}
	
	[OnWindowDisable]
	public void WindowDisable(){
		Debug.Log("test window disabled");
	}
	
}