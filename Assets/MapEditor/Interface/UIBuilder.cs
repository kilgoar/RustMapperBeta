using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;


[AttributeUsage(AttributeTargets.Field | AttributeTargets.Method)]
public class BeginWindow : Attribute
{
    public string Title { get; }
    public BeginWindow(string title = "Dynamic UI Window")        {
        Title = title;
    }
}

[AttributeUsage(AttributeTargets.Method)]
public class OnWindowEnable : Attribute { }

[AttributeUsage(AttributeTargets.Method)]
public class OnWindowDisable : Attribute { }

[AttributeUsage(AttributeTargets.Field)]
public class OnBind : Attribute
{
    public string Method { get; }
    public OnBind(string method)
    {
        Method = method;
    }
}

[AttributeUsage(AttributeTargets.Field)]
public class SliderUIElement : Attribute
{
    public float Min { get; }
    public float Max { get; }
    public bool Whole { get; }
    public string Label { get; } // Optional label override

    public SliderUIElement(float min = 0f, float max = 1f, bool whole = false, string label = null)
    {
        Min = min;
        Max = max;
        Whole = whole;
        Label = label;
    }
}

[AttributeUsage(AttributeTargets.Field)]
public class RangeSlideMin : Attribute
{
    public float Min { get; }
    public float Max { get; }
    public bool Whole { get; }
    public string Label { get; } // UI title/label

    public RangeSlideMin(float min = 0f, float max = 1f, bool whole = false, string label = null)
    {
        Min = min;
        Max = max;
        Whole = whole;
        Label = label;
    }
}

[AttributeUsage(AttributeTargets.Field)]
public class RangeSlideMax : Attribute { } // Marker only—no params

[AttributeUsage(AttributeTargets.Field)]
public class RustMapperUIElement : Attribute
{
    public string Label { get; }
    public RustMapperUIElement(string label = null)        {
        Label = label;
    }
}

[AttributeUsage(AttributeTargets.Method)]
public class RustMapperButton : Attribute    {
    public string Label { get; }
    public RustMapperButton(string label = null)        {
        Label = label;
    }
}

[AttributeUsage(AttributeTargets.Method)]
public class Sync : Attribute {		}

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Method)]
public class HorizontalGroup : Attribute    {    }

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Method)]
public class EndHorizontalGroup : Attribute    {    }




public abstract class UIBuilder : MonoBehaviour
{
    public GameObject horizontalContainer;
    public Toggle templateToggle;
    public Text label;
    public Button button, buttonbright;
    public Slider slider;	
    public RangeSlide rangeSlide;
    public Dropdown dropdown;
    public Vector3Field vector3Fields;
    public InputField inputField;
	public ListView listView;

    protected Dictionary<FieldInfo, Component> fieldToUIElement = new Dictionary<FieldInfo, Component>();

    // Create a Label Text
    protected Text CreateLabelText(Transform parent, string text = "")
    {
        if (label == null)
        {
            Debug.LogError($"{GetType().Name}'s label field is not assigned.");
            return null;
        }
        Text newText = Instantiate(label, parent, false);
        newText.text = text;
		newText.gameObject.SetActive(true);
        return newText;
    }
	
	private MethodInfo ResolveOnBind(FieldInfo field, object target)
    {
        var onBindAttr = field.GetCustomAttribute<OnBind>();
        if (onBindAttr != null)
        {
            var method = target.GetType().GetMethod(onBindAttr.Method, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (method == null || method.ReturnType != typeof(void) || method.GetParameters().Length != 0)
            {
                Debug.LogError($"Invalid [OnBind] on {field.Name}: Method '{onBindAttr.Method}' not found or invalid signature (must be parameterless void).");
                return null;
            }
            return method;
        }
        return null;
    }
	
    // Updated slider binding to support OnBind (invokes on value changed)
    protected Slider CreateAndBindSlider(Transform parent, FieldInfo field, object target, float minVal, float maxVal, bool whole, string title)
    {
        if (slider == null)
        {
            Debug.LogError("UIBuilder's slider field is not assigned.");
            return null;
        }

        Slider newSlider = Instantiate(slider, parent, false);
        newSlider.gameObject.SetActive(true);

        newSlider.minValue = minVal;
        newSlider.maxValue = maxVal;
        newSlider.wholeNumbers = whole;

        // Initial value
        float initialValue = 0f;
        if (field.FieldType == typeof(float))
            initialValue = (float)field.GetValue(target);
        else if (field.FieldType == typeof(int))
            initialValue = (int)field.GetValue(target);
        newSlider.value = Mathf.Clamp(initialValue, minVal, maxVal);

        // Resolve bound method from [OnBind] if present
        MethodInfo boundMethod = ResolveOnBind(field, target);

        // Write-back on change
        newSlider.onValueChanged.AddListener((value) =>
        {
            try
            {
                if (field.FieldType == typeof(float))
                    field.SetValue(target, value);
                else if (field.FieldType == typeof(int))
                    field.SetValue(target, whole ? Mathf.RoundToInt(value) : (int)value);
                Debug.Log($"{field.Name} slider set to: {value}");

                // Invoke bound method if any
                boundMethod?.Invoke(target, null);
            }
            catch (Exception e)
            {
                Debug.LogError($"Error binding slider {field.Name}: {e.Message}");
            }
        });

        fieldToUIElement[field] = newSlider;
        return newSlider;
    }

    // Updated range slide binding to support OnBind (invokes on slider released, per low/high field)
    public RangeSlide CreateAndBindRangeSlide(Transform parent, FieldInfo lowField, FieldInfo highField, object targetObject, float minVal, float maxVal, bool whole, string title)
    {
        if (rangeSlide == null)
        {
            Debug.LogError("UIBuilder's rangeSlide field is not assigned.");
            return null;
        }

        RangeSlide newRange = Instantiate(rangeSlide, parent, false);
        newRange.gameObject.SetActive(true);

        // Runtime config SO (populated from attribute – no disk asset)
        SliderFieldSO runtimeSO = ScriptableObject.CreateInstance<SliderFieldSO>();
        runtimeSO.minSetting = minVal;
        runtimeSO.maxSetting = maxVal;
        runtimeSO.whole = whole;
        runtimeSO.title = "";
        
        runtimeSO.name = $"RuntimeRangeSO_{lowField.Name}";
        runtimeSO.hideFlags = HideFlags.DontSave | HideFlags.HideInHierarchy;

        newRange.sfData = runtimeSO;
        newRange.Init(); // Your existing init sets up sliders/inputs/title

        // Bind initial values
        float lowVal = (float)lowField.GetValue(targetObject);
        float highVal = (float)highField.GetValue(targetObject);
        newRange.lowSlider.value = Mathf.Clamp(lowVal, minVal, maxVal);
        newRange.highSlider.value = Mathf.Clamp(highVal, minVal, maxVal);
        newRange.SliderChanged(); // Sync fields and clamps

        // Resolve bound methods
        MethodInfo lowBoundMethod = ResolveOnBind(lowField, targetObject);
        MethodInfo highBoundMethod = ResolveOnBind(highField, targetObject);

        // Write-back on release
        newRange.onSliderReleased.AddListener(() =>
        {
            float low = newRange.lowSlider.value;
            float high = newRange.highSlider.value;
            lowField.SetValue(targetObject, whole ? Mathf.RoundToInt(low) : low);
            highField.SetValue(targetObject, whole ? Mathf.RoundToInt(high) : high);
            Debug.Log($"{lowField.Name}/{highField.Name} set to [{low}, {high}]");
            
            // Invoke bound methods if any
            lowBoundMethod?.Invoke(targetObject, null);
            highBoundMethod?.Invoke(targetObject, null);
        });

        fieldToUIElement[lowField] = newRange; // Key on Low for dictionary
        return newRange;
    }
	

    protected ListView CreateAndBindListView(Transform parent, FieldInfo field, object target)
    {
        if (listView == null)
        {
            Debug.LogError($"{GetType().Name}'s listView field is not assigned.");
            return null;
        }

        // Instantiate ListView from the child component
        ListView newListView = Instantiate(listView.gameObject, parent, false).GetComponent<ListView>();
		
        object listValue = field.GetValue(target);

        if (listValue != null)
        {
            newListView.Build(listValue);
            fieldToUIElement[field] = newListView;
        }
        else
        {
            Debug.LogWarning($"List field {field.Name} is null. Initializing empty ListView.");
            newListView.Build(Activator.CreateInstance(field.FieldType));
        }

        return newListView;
    }
	
    // Create and bind an InputField
    protected InputField CreateAndBindInputField(Transform parent, FieldInfo field, object target)
    {
        if (inputField == null)
        {
            Debug.LogError($"{GetType().Name}'s inputField field is not assigned.");
            return null;
        }
        InputField newInputField = Instantiate(inputField, parent, false);
		
        // Set initial value and content type
        if (field.FieldType == typeof(string))
        {
            newInputField.text = (string)field.GetValue(target) ?? "";
            newInputField.contentType = InputField.ContentType.Standard;
        }
        else if (field.FieldType == typeof(int))
        {
            newInputField.text = ((int)field.GetValue(target)).ToString();
            newInputField.contentType = InputField.ContentType.IntegerNumber;
        }
        else if (field.FieldType == typeof(uint))
        {
            newInputField.text = ((uint)field.GetValue(target)).ToString();
            newInputField.contentType = InputField.ContentType.IntegerNumber;
        }
        else if (field.FieldType == typeof(float))
        {
            newInputField.text = ((float)field.GetValue(target)).ToString("F2");
            newInputField.contentType = InputField.ContentType.DecimalNumber;
        }
        else
        {
            Debug.LogError($"Unsupported field type {field.FieldType} for InputField binding.");
            Destroy(newInputField.gameObject);
            return null;
        }

        // Bind UI-to-object changes
        newInputField.onValueChanged.AddListener((value) =>
        {
            try
            {
                if (field.FieldType == typeof(string))
                {
                    field.SetValue(target, value);
                    Debug.Log($"{field.Name} set to: {value}");
                }
                else if (field.FieldType == typeof(int) && int.TryParse(value, out int intResult))
                {
                    field.SetValue(target, intResult);
                    Debug.Log($"{field.Name} set to: {intResult}");
                }
                else if (field.FieldType == typeof(uint) && uint.TryParse(value, out uint uintResult))
                {
                    field.SetValue(target, uintResult);
                    Debug.Log($"{field.Name} set to: {uintResult}");
                }
                else if (field.FieldType == typeof(float) && float.TryParse(value, out float floatResult))
                {
                    field.SetValue(target, floatResult);
                    Debug.Log($"{field.Name} set to: {floatResult}");
                }
                else
                {
                    // Revert to current field value if parsing fails
                    if (field.FieldType == typeof(string))
                        newInputField.text = (string)field.GetValue(target) ?? "";
                    else if (field.FieldType == typeof(int))
                        newInputField.text = ((int)field.GetValue(target)).ToString();
                    else if (field.FieldType == typeof(uint))
                        newInputField.text = ((uint)field.GetValue(target)).ToString();
                    else if (field.FieldType == typeof(float))
                        newInputField.text = ((float)field.GetValue(target)).ToString("F2");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error binding {field.Name}: {e.Message}");
            }
        });

        return newInputField;
    }

	protected Toggle CreateAndBindToggle(Transform parent, FieldInfo field, object target)
	{
		if (templateToggle == null)
		{
			Debug.LogError($"{GetType().Name}'s templateToggle field is not assigned.");
			return null;
		}

		Toggle newToggle = Instantiate(templateToggle, parent, false);

		if (field.FieldType == typeof(bool))
		{
			newToggle.isOn = (bool)field.GetValue(target);
		}
		else
		{
			Debug.LogError($"Unsupported field type {field.FieldType} for Toggle binding.");
			Destroy(newToggle.gameObject);
			return null;
		}

		// Resolve bound method from [OnBind] if present
		MethodInfo boundMethod = ResolveOnBind(field, target);

		newToggle.onValueChanged.AddListener((value) =>
		{
			try
			{
				field.SetValue(target, value);
				Debug.Log($"{field.Name} set to: {value}");

				// Invoke bound method if any
				if (boundMethod != null)
				{
					boundMethod.Invoke(target, null);
					Debug.Log($"Invoked [OnBind] method {boundMethod.Name} for {field.Name}");
				}
			}
			catch (Exception e)
			{
				Debug.LogError($"Error binding {field.Name}: {e.Message}");
			}
		});

		return newToggle;
	}

	protected Dropdown CreateAndBindDropdown(Transform parent, FieldInfo field, object target)
	{
		if (dropdown == null)
		{
			Debug.LogError($"{GetType().Name}'s dropdown field is not assigned.");
			return null;
		}

		Dropdown newDropdown = Instantiate(dropdown, parent, false);

		if (field.FieldType.IsEnum)
		{
			string[] enumNames = Enum.GetNames(field.FieldType);
			newDropdown.options.Clear();
			foreach (string name in enumNames)
				newDropdown.options.Add(new Dropdown.OptionData(name));

			object currentValue = field.GetValue(target);
			int index = Array.IndexOf(enumNames, currentValue.ToString());
			newDropdown.value = index >= 0 ? index : 0;

			// Resolve bound method from [OnBind] if present
			MethodInfo boundMethod = ResolveOnBind(field, target);

			newDropdown.onValueChanged.AddListener((value) =>
			{
				try
				{
					object enumValue = Enum.Parse(field.FieldType, enumNames[value]);
					field.SetValue(target, enumValue);
					Debug.Log($"{field.Name} set to: {enumValue}");

					// Invoke bound method if any
					if (boundMethod != null)
					{
						boundMethod.Invoke(target, null);
						Debug.Log($"Invoked [OnBind] method {boundMethod.Name} for {field.Name}");
					}
				}
				catch (Exception e)
				{
					Debug.LogError($"Error binding {field.Name}: {e.Message}");
					object currentEnumValue = field.GetValue(target);
					int currentIndex = Array.IndexOf(enumNames, currentEnumValue.ToString());
					if (currentIndex >= 0)
						newDropdown.value = currentIndex;
				}
			});
		}
		else
		{
			Debug.LogError($"Unsupported field type {field.FieldType} for Dropdown binding.");
			Destroy(newDropdown.gameObject);
			return null;
		}

		return newDropdown;
	}

    // Create and bind a Vector3Field
    protected Vector3Field CreateAndBindVector3Field(Transform parent, FieldInfo field, object target)
    {
        if (vector3Fields == null)
        {
            Debug.LogError($"{GetType().Name}'s vector3Fields field is not assigned.");
            return null;
        }

        Vector3Field newVector3Field = Instantiate(vector3Fields, parent, false);

        if (field.FieldType == typeof(Vector3))
        {
            Vector3 currentValue = (Vector3)field.GetValue(target);
            newVector3Field.SetVector3(currentValue);

            UnityAction<string> updateVector3 = (_) =>
            {
                try
                {
                    Vector3 newValue = newVector3Field.GetVector3();
                    field.SetValue(target, newValue);
                    Debug.Log($"{field.Name} set to: {newValue}");
                }
                catch (Exception e)
                {
                    Debug.LogError($"Error binding {field.Name}: {e.Message}");
                    Vector3 currentValue = (Vector3)field.GetValue(target);
                    newVector3Field.SetVector3(currentValue);
                }
            };

            newVector3Field.xField.onEndEdit.AddListener(updateVector3);
            newVector3Field.yField.onEndEdit.AddListener(updateVector3);
            newVector3Field.zField.onEndEdit.AddListener(updateVector3);
        }
        else
        {
            Debug.LogError($"Unsupported field type {field.FieldType} for Vector3Field binding.");
            Destroy(newVector3Field.gameObject);
            return null;
        }

        return newVector3Field;
    }

    protected Button CreateAndBindButton(Transform parent, MethodInfo method, object target, string labelText)
    {
        if (button == null)
        {
            Debug.LogError($"{GetType().Name}'s button field is not assigned.");
            return null;
        }

        Button newButton = Instantiate(button, parent, false);
        Text label = newButton.GetComponentInChildren<Text>();
        if (label != null)
            label.text = labelText;
        else if (!string.IsNullOrEmpty(labelText))
            Debug.LogWarning("Button prefab has no child Text component to set label.");

        newButton.onClick.AddListener(() =>
        {
            try
            {
                method.Invoke(target, null);
                Debug.Log($"Invoked method: {method.Name}");

                // Check if the method has the [Sync] attribute and if this is a TemplateWindow
                if (method.GetCustomAttribute<Sync>() != null && this is TemplateWindow templateWindow)
                {
                    templateWindow.SyncWindow();
                    Debug.Log($"SyncWindow called after invoking {method.Name}");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error invoking {method.Name}: {e.Message}");
            }
        });

        return newButton;
    }
	
	// Enable or disable all template UI elements
	public void EnableTemplates(bool isEnabled)
	{
		// Array of all template UI elements
		GameObject[] templates = new GameObject[]
		{
			horizontalContainer,
			templateToggle != null ? templateToggle.gameObject : null,
			label != null ? label.gameObject : null,
			button != null ? button.gameObject : null,
			buttonbright != null ? buttonbright.gameObject : null,
			slider != null ? slider.gameObject : null,
			dropdown != null ? dropdown.gameObject : null,
			vector3Fields != null ? vector3Fields.gameObject : null,
			inputField != null ? inputField.gameObject : null,
			listView != null ? listView.gameObject : null
		};

		// Set active state for each non-null template
		foreach (var template in templates)
		{
			if (template != null)
			{
				template.SetActive(isEnabled);
			}
		}
	}
}