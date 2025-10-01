using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

public class TemplateWindow : UIBuilder
{
    public Text title, footer;
	public Toggle toggle;
    public Button close, rescale;
    public GameObject itemView;
    public RangeSlide rangeSlide;
    public object targetObject;

    public GameObject CreateItemView(Transform parent)
    {
        if (itemView == null)
        {
            Debug.LogError("TemplateWindow's itemView field is not assigned.");
            return null;
        }
        return Instantiate(itemView, parent, false);
    }

    public void Build(object target)
    {

        if (target == null)
        {
            Debug.LogError("Target object is null. Cannot build UI.");
            return;
        }
		
		EnableTemplates(true);
		
        targetObject = target;
        Type type = targetObject.GetType();
        title.text = type.Name;

        fieldToUIElement.Clear();

        var members = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .Cast<MemberInfo>()
            .Concat(type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).Cast<MemberInfo>())
            .OrderBy(m => m.MetadataToken)
            .ToList();

        int? beginToken = null;
        string windowTitle = type.Name;
        foreach (var member in members)
        {
            var beginAttr = member.GetCustomAttribute<BeginWindow>();
            if (beginAttr != null && beginToken == null)
            {
                beginToken = member.MetadataToken;
                windowTitle = beginAttr.Title;
                title.text = windowTitle;
            }
        }

        GameObject scrollViewObj = CreateItemView(transform);
        if (scrollViewObj == null) return;

        ScrollRect scrollRect = scrollViewObj.GetComponent<ScrollRect>();
        Transform contentTransform = scrollRect.content;
        if (contentTransform == null)
        {
            Debug.LogError("ScrollRect content is not assigned.");
            return;
        }

        RectTransform contentRect = contentTransform.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0, 1);
        contentRect.anchorMax = new Vector2(1, 1);
        contentRect.pivot = new Vector2(0, 1);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = new Vector2(0, 0);

        VerticalLayoutGroup verticalLayout = contentTransform.GetComponent<VerticalLayoutGroup>();
        if (verticalLayout == null)
        {
            verticalLayout = contentTransform.gameObject.AddComponent<VerticalLayoutGroup>();
            verticalLayout.padding = new RectOffset(5, 5, 5, 5);
            verticalLayout.spacing = 5;
            verticalLayout.childAlignment = TextAnchor.UpperLeft;
            verticalLayout.childControlWidth = true;
            verticalLayout.childControlHeight = false;
            verticalLayout.childForceExpandWidth = true;
            verticalLayout.childForceExpandHeight = false;
        }

        int elementCount = 0;
        Stack<GameObject> horizontalContainerStack = new Stack<GameObject>();
        float contentHeight = 0;
        float contentSpacing = 5;
        List<GameObject> horizontalContainers = new List<GameObject>();

        foreach (var member in members)
        {
            if (beginToken != null && member.MetadataToken < beginToken)
                continue;

            bool hasHorizontalGroup = member.GetCustomAttribute<HorizontalGroup>() != null;
            bool hasEndHorizontalGroup = member.GetCustomAttribute<EndHorizontalGroup>() != null;

            Transform elementParent = horizontalContainerStack.Count > 0 ? horizontalContainerStack.Peek().transform : contentTransform;

            if (hasHorizontalGroup)
            {
                if (horizontalContainer == null)
                {
                    Debug.LogError("TemplateWindow's horizontalContainer field is not assigned.");
                    return;
                }
                GameObject newHorizontalContainer = Instantiate(horizontalContainer, elementParent, false);
                newHorizontalContainer.name = $"{member.Name}_HorizontalContainer";
                horizontalContainers.Add(newHorizontalContainer);
                horizontalContainerStack.Push(newHorizontalContainer);
                elementParent = newHorizontalContainer.transform;
                if (horizontalContainerStack.Count == 1)
                    contentHeight += 50 + contentSpacing;
            }

            if (member is FieldInfo field)
            {
                RustMapperUIElement attr = field.GetCustomAttribute<RustMapperUIElement>();
                string labelText = attr?.Label ?? field.Name;

                bool isLabel = field.FieldType == typeof(string) && (field.IsInitOnly || field.IsLiteral || (field.GetCustomAttribute<System.Runtime.CompilerServices.IsReadOnlyAttribute>() != null));

                if (isLabel)
                {
                    Text newText = CreateLabelText(elementParent, (string)field.GetValue(targetObject) ?? labelText);
                    if (newText != null)
                    {
                        fieldToUIElement[field] = newText;
                        if (horizontalContainerStack.Count == 0)
                            contentHeight += newText.GetComponent<RectTransform>().sizeDelta.y + contentSpacing;
                    }
                    elementCount++;
                }
                else if (field.FieldType.IsGenericType && field.FieldType.GetGenericTypeDefinition() == typeof(List<>))
				{
					if (horizontalContainerStack.Count == 0)
					{
						if (horizontalContainer == null)
						{
							Debug.LogError("TemplateWindow's horizontalContainer field is not assigned.");
							return;
						}
						GameObject newHorizontalContainer = Instantiate(horizontalContainer, contentTransform, false);
						newHorizontalContainer.name = $"{field.Name}_HorizontalContainer";
						horizontalContainers.Add(newHorizontalContainer);
						horizontalContainerStack.Push(newHorizontalContainer);
						elementParent = newHorizontalContainer.transform;
						contentHeight += 100 + contentSpacing; // Increased height for ListView
					}

					// Skip creating external label for ListView since it has its own
					// CreateLabelText(elementParent, labelText); // Removed

					ListView listView = CreateAndBindListView(elementParent, field, targetObject);
					if (listView != null)
					{
						if (horizontalContainerStack.Count == 0)
							contentHeight += listView.GetComponent<RectTransform>().sizeDelta.y + contentSpacing;
						elementCount++;
					}

					if (horizontalContainerStack.Count > 0 && !hasHorizontalGroup && elementParent.gameObject == horizontalContainerStack.Peek())
					{
						horizontalContainerStack.Pop();
						elementCount++;
					}
				}
                else
                {
                    if (horizontalContainerStack.Count == 0)
                    {
                        if (horizontalContainer == null)
                        {
                            Debug.LogError("TemplateWindow's horizontalContainer field is not assigned.");
                            return;
                        }
                        GameObject newHorizontalContainer = Instantiate(horizontalContainer, contentTransform, false);
                        newHorizontalContainer.name = $"{field.Name}_HorizontalContainer";
                        horizontalContainers.Add(newHorizontalContainer);
                        horizontalContainerStack.Push(newHorizontalContainer);
                        elementParent = newHorizontalContainer.transform;
                        contentHeight += 50 + contentSpacing;
                    }

                    CreateLabelText(elementParent, labelText);

                    Component uiElement = null;
                    if (field.FieldType == typeof(string) || field.FieldType == typeof(int) || field.FieldType == typeof(uint) || field.FieldType == typeof(float))
                        uiElement = CreateAndBindInputField(elementParent, field, targetObject);
                    else if (field.FieldType == typeof(bool))
                        uiElement = CreateAndBindToggle(elementParent, field, targetObject);
                    else if (field.FieldType.IsEnum)
                        uiElement = CreateAndBindDropdown(elementParent, field, targetObject);
                    else if (field.FieldType == typeof(Vector3))
                        uiElement = CreateAndBindVector3Field(elementParent, field, targetObject);
                    else
                    {
                        Debug.LogWarning($"Unsupported field type {field.FieldType} for field {field.Name}");
                        if (horizontalContainerStack.Count > 0 && horizontalContainerStack.Peek() == elementParent.gameObject)
                            horizontalContainerStack.Pop();
                        continue;
                    }

                    if (uiElement != null)
                        fieldToUIElement[field] = uiElement;

                    if (horizontalContainerStack.Count > 0 && !hasHorizontalGroup && elementParent.gameObject == horizontalContainerStack.Peek())
                    {
                        horizontalContainerStack.Pop();
                        elementCount++;
                    }
                }
            }
            else if (member is MethodInfo method && method.ReturnType == typeof(void) && method.GetParameters().Length == 0)
            {
                RustMapperButton attr = member.GetCustomAttribute<RustMapperButton>();
                string labelText = attr?.Label ?? method.Name;

                Button button = CreateAndBindButton(elementParent, method, targetObject, labelText);
                if (button != null)
                {
                    if (horizontalContainerStack.Count == 0)
                        contentHeight += button.GetComponent<RectTransform>().sizeDelta.y + contentSpacing;
                    elementCount++;
                }
            }

            if (hasEndHorizontalGroup)
            {
                if (horizontalContainerStack.Count == 0)
                    Debug.LogWarning($"[EndHorizontalGroup] without matching [HorizontalGroup] at {member.Name}. Ignoring.");
                else
                {
                    horizontalContainerStack.Pop();
                    if (horizontalContainerStack.Count == 0)
                        elementCount++;
                }
            }
        }

        while (horizontalContainerStack.Count > 0)
        {
            Debug.LogWarning($"Unclosed [HorizontalGroup] detected. Closing automatically.");
            horizontalContainerStack.Pop();
            elementCount++;
        }

        foreach (var container in horizontalContainers)
        {
            if (container.transform.childCount == 0)
            {
                Debug.LogWarning($"Removing empty horizontal container: {container.name}");
                contentHeight -= 50 + contentSpacing;
                Destroy(container);
            }
        }

        contentRect.sizeDelta = new Vector2(contentRect.sizeDelta.x, contentHeight);

        foreach (var component in gameObject.GetComponentsInChildren<Component>(true))
        {
            if (component is Behaviour behaviour)
                behaviour.enabled = true;
        }

        Debug.Log($"Generated UI with {elementCount} elements for {type.Name}");
		EnableTemplates(false);
    }

/*
    private void Update()
    {
        if (targetObject == null) return;

        foreach (var pair in fieldToUIElement)
        {
            FieldInfo field = pair.Key;
            Component uiElement = pair.Value;

            try
            {
                if (uiElement is Text text && field.FieldType == typeof(string) && (field.IsInitOnly || field.IsLiteral || (field.GetCustomAttribute<System.Runtime.CompilerServices.IsReadOnlyAttribute>() != null)))
                {
                    string currentValue = (string)field.GetValue(targetObject) ?? "";
                    if (text.text != currentValue)
                        text.text = currentValue;
                }
                else if (uiElement is InputField inputField)
                {
                    string currentValue = null;
                    if (field.FieldType == typeof(string))
                        currentValue = (string)field.GetValue(targetObject) ?? "";
                    else if (field.FieldType == typeof(int))
                        currentValue = ((int)field.GetValue(targetObject)).ToString();
                    else if (field.FieldType == typeof(uint))
                        currentValue = ((uint)field.GetValue(targetObject)).ToString();
                    else if (field.FieldType == typeof(float))
                        currentValue = ((float)field.GetValue(targetObject)).ToString("F2");

                    if (currentValue != null && inputField.text != currentValue)
                        inputField.text = currentValue;
                }
                else if (uiElement is Toggle toggle && field.FieldType == typeof(bool))
                {
                    bool currentValue = (bool)field.GetValue(targetObject);
                    if (toggle.isOn != currentValue)
                        toggle.isOn = currentValue;
                }
                else if (uiElement is Dropdown dropdown && field.FieldType.IsEnum)
                {
                    object currentValue = field.GetValue(targetObject);
                    string[] enumNames = Enum.GetNames(field.FieldType);
                    int index = Array.IndexOf(enumNames, currentValue.ToString());
                    if (index >= 0 && dropdown.value != index)
                        dropdown.value = index;
                }
                else if (uiElement is Vector3Field vector3Field && field.FieldType == typeof(Vector3))
                {
                    Vector3 currentValue = (Vector3)field.GetValue(targetObject);
                    Vector3 uiValue = vector3Field.GetVector3();
                    if (currentValue != uiValue)
                        vector3Field.SetVector3(currentValue);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error updating UI for {field.Name}: {e.Message}");
            }
        }
    }
	*/

    // Non-binding UI creation methods
    public Button CreateButton(Transform parent, Rect rect, string text = "", UnityAction onClick = null)
    {
        if (button == null)
        {
            Debug.LogError("TemplateWindow's button field is not assigned.");
            return null;
        }
        Button newButton = Instantiate(button, parent, false);
        RectTransform buttonRect = newButton.GetComponent<RectTransform>();
        buttonRect.anchoredPosition = new Vector2(rect.x, rect.y);
        buttonRect.sizeDelta = new Vector2(rect.width, rect.height);

        Text label = newButton.GetComponentInChildren<Text>();
        if (label != null)
            label.text = text;
        else if (!string.IsNullOrEmpty(text))
            Debug.LogWarning("Button prefab has no child Text component to set label.");

        if (onClick != null)
            newButton.onClick.AddListener(onClick);

        return newButton;
    }

    public Button CreateBrightButton(Transform parent, Rect rect, string text = "")
    {
        if (buttonbright == null)
        {
            Debug.LogError("TemplateWindow's buttonbright field is not assigned.");
            return null;
        }
        Button newButton = Instantiate(buttonbright, parent, false);
        RectTransform buttonRect = newButton.GetComponent<RectTransform>();
        buttonRect.anchoredPosition = new Vector2(rect.x, rect.y);
        buttonRect.sizeDelta = new Vector2(rect.width, rect.height);

        Text label = newButton.GetComponentInChildren<Text>();
        if (label != null)
            label.text = text;
        else if (!string.IsNullOrEmpty(text))
            Debug.LogWarning("Bright Button prefab has no child Text component to set label.");

        return newButton;
    }

    public Slider CreateSlider(Transform parent, Rect rect)
    {
        if (slider == null)
        {
            Debug.LogError("TemplateWindow's slider field is not assigned.");
            return null;
        }
        Slider newSlider = Instantiate(slider, parent, false);
        RectTransform sliderRect = newSlider.GetComponent<RectTransform>();
        sliderRect.anchoredPosition = new Vector2(rect.x, rect.y);
        sliderRect.sizeDelta = new Vector2(rect.width, rect.height);
        return newSlider;
    }

    public Dropdown CreateDropdown(Transform parent, Rect rect)
    {
        if (dropdown == null)
        {
            Debug.LogError("TemplateWindow's dropdown field is not assigned.");
            return null;
        }
        Dropdown newDropdown = Instantiate(dropdown, parent, false);
        RectTransform dropdownRect = newDropdown.GetComponent<RectTransform>();
        dropdownRect.anchoredPosition = new Vector2(rect.x, rect.y);
        dropdownRect.sizeDelta = new Vector2(rect.width, rect.height);
        return newDropdown;
    }

    public InputField CreateInputField(Transform parent, Rect rect, string text = "")
    {
        if (inputField == null)
        {
            Debug.LogError("TemplateWindow's inputField field is not assigned.");
            return null;
        }
        InputField newInputField = Instantiate(inputField, parent, false);
        RectTransform inputRect = newInputField.GetComponent<RectTransform>();
        inputRect.anchoredPosition = new Vector2(rect.x, rect.y);
        inputRect.sizeDelta = new Vector2(rect.width, rect.height);
        newInputField.text = text;
        return newInputField;
    }
	
	public void SyncWindow()
    {
        if (targetObject == null)
        {
            Debug.LogWarning("Cannot sync window: targetObject is null.");
            return;
        }

        foreach (var pair in fieldToUIElement)
        {
            FieldInfo field = pair.Key;
            Component uiElement = pair.Value;

            try
            {
                if (uiElement is Text text && field.FieldType == typeof(string) && 
                    (field.IsInitOnly || field.IsLiteral || field.GetCustomAttribute<System.Runtime.CompilerServices.IsReadOnlyAttribute>() != null))
                {
                    string currentValue = (string)field.GetValue(targetObject) ?? "";
                    if (text.text != currentValue)
                    {
                        text.text = currentValue;
                        Debug.Log($"Synced Text for {field.Name} to: {currentValue}");
                    }
                }
                else if (uiElement is InputField inputField)
                {
                    string currentValue = null;
                    if (field.FieldType == typeof(string))
                        currentValue = (string)field.GetValue(targetObject) ?? "";
                    else if (field.FieldType == typeof(int))
                        currentValue = ((int)field.GetValue(targetObject)).ToString();
                    else if (field.FieldType == typeof(uint))
                        currentValue = ((uint)field.GetValue(targetObject)).ToString();
                    else if (field.FieldType == typeof(float))
                        currentValue = ((float)field.GetValue(targetObject)).ToString("F2");

                    if (currentValue != null && inputField.text != currentValue)
                    {
                        inputField.text = currentValue;
                        Debug.Log($"Synced InputField for {field.Name} to: {currentValue}");
                    }
                }
                else if (uiElement is Toggle toggle && field.FieldType == typeof(bool))
                {
                    bool currentValue = (bool)field.GetValue(targetObject);
                    if (toggle.isOn != currentValue)
                    {
                        toggle.isOn = currentValue;
                        Debug.Log($"Synced Toggle for {field.Name} to: {currentValue}");
                    }
                }
                else if (uiElement is Dropdown dropdown && field.FieldType.IsEnum)
                {
                    object currentValue = field.GetValue(targetObject);
                    string[] enumNames = Enum.GetNames(field.FieldType);
                    int index = Array.IndexOf(enumNames, currentValue.ToString());
                    if (index >= 0 && dropdown.value != index)
                    {
                        dropdown.value = index;
                        Debug.Log($"Synced Dropdown for {field.Name} to: {currentValue}");
                    }
                }
                else if (uiElement is Vector3Field vector3Field && field.FieldType == typeof(Vector3))
                {
                    Vector3 currentValue = (Vector3)field.GetValue(targetObject);
                    Vector3 uiValue = vector3Field.GetVector3();
                    if (currentValue != uiValue)
                    {
                        vector3Field.SetVector3(currentValue);
                        Debug.Log($"Synced Vector3Field for {field.Name} to: {currentValue}");
                    }
                }
                else if (uiElement is ListView listView && field.FieldType.IsGenericType && 
                         field.FieldType.GetGenericTypeDefinition() == typeof(List<>))
                {
                    listView.SyncList();
                    Debug.Log($"Synced ListView for {field.Name}");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error syncing UI for {field.Name}: {e.Message}");
            }
        }
    }
	
}