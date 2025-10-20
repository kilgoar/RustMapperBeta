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
    public object targetObject;
	
	// Store bound lifecycle methods
    private MethodInfo onEnableMethod;
    private MethodInfo onDisableMethod;

    public GameObject CreateItemView(Transform parent)
    {
        if (itemView == null)
        {
            Debug.LogError("TemplateWindow's itemView field is not assigned.");
            return null;
        }
        return Instantiate(itemView, parent, false);
    }


    private Stack<GameObject> explicitHorizontalGroupStack = new Stack<GameObject>();
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
        .Concat(type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
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
        verticalLayout.childControlWidth = false;
        verticalLayout.childForceExpandWidth = false;
        verticalLayout.childControlHeight = false;
        verticalLayout.childForceExpandHeight = false;
    }

    int elementCount = 0;
    float contentHeight = 0;
    float contentSpacing = 5;
    float maxContentWidth = 0;
    List<GameObject> horizontalContainers = new List<GameObject>();
    FieldInfo pendingMinField = null;
    RangeSlideMin pendingMinAttr = null;

    foreach (var member in members)
    {
        if (beginToken != null && member.MetadataToken < beginToken)
            continue;

        // Determine the parent based on the current stack state
        Transform elementParent = explicitHorizontalGroupStack.Count > 0 
            ? explicitHorizontalGroupStack.Peek().transform 
            : contentTransform;

        // Check for group attributes
        bool hasHorizontalGroup = member.GetCustomAttribute<HorizontalGroup>() != null;
        bool hasEndHorizontalGroup = member.GetCustomAttribute<EndHorizontalGroup>() != null;

        // Handle [EndHorizontalGroup] first if present
        if (hasEndHorizontalGroup)
        {
            if (explicitHorizontalGroupStack.Count == 0)
            {
                Debug.LogWarning($"[EndHorizontalGroup] without matching [HorizontalGroup] at {member.Name}. Ignoring.");
            }
            else
            {
                GameObject closedContainer = explicitHorizontalGroupStack.Pop();
                elementCount++;
            }
        }

        // Handle [HorizontalGroup] after closing any previous group
        if (hasHorizontalGroup)
        {
            if (horizontalContainer == null)
            {
                Debug.LogError("TemplateWindow's horizontalContainer field is not assigned.");
                return;
            }
            GameObject newHorizontalContainer = Instantiate(horizontalContainer, contentTransform, false);
            newHorizontalContainer.name = $"{member.Name}_HorizontalGroup";
            horizontalContainers.Add(newHorizontalContainer);
            explicitHorizontalGroupStack.Push(newHorizontalContainer);

            HorizontalLayoutGroup layoutGroup = newHorizontalContainer.GetComponent<HorizontalLayoutGroup>();


            contentHeight += 50 + contentSpacing;
            Debug.Log($"Opened [HorizontalGroup] for {member.Name}: {newHorizontalContainer.name}");

            // Update elementParent to the new group for the current field
            elementParent = newHorizontalContainer.transform;
        }

        // Process fields
        if (member is FieldInfo field)
        {
            RustMapperUIElement attr = field.GetCustomAttribute<RustMapperUIElement>();
            string labelText = attr?.Label ?? field.Name;

            bool isLabel = field.FieldType == typeof(string) && 
                (field.IsInitOnly || field.IsLiteral || field.GetCustomAttribute<System.Runtime.CompilerServices.IsReadOnlyAttribute>() != null);

            if (isLabel)
            {
                Text newText = CreateLabelText(elementParent, (string)field.GetValue(targetObject) ?? labelText);
                if (newText != null)
                {
                    fieldToUIElement[field] = newText;
                    if (explicitHorizontalGroupStack.Count == 0)
                    {
                        contentHeight += newText.GetComponent<RectTransform>().sizeDelta.y + contentSpacing;
                        maxContentWidth = Mathf.Max(maxContentWidth, newText.GetComponent<RectTransform>().sizeDelta.x);
                    }
                    elementCount++;
                    Debug.Log($"Created label for {field.Name} under {elementParent.name}");
                }
            }
            else if (field.GetCustomAttribute<RangeSlideMin>() is RangeSlideMin minAttr && field.FieldType == typeof(float))
            {
                pendingMinField = field;
                pendingMinAttr = minAttr;
                Debug.Log($"Pending [RangeSlideMin] for {field.Name}");
                continue;
            }
            else if (field.GetCustomAttribute<RangeSlideMax>() != null && field.FieldType == typeof(float))
            {
                if (pendingMinField != null && pendingMinAttr != null)
                {
                    if (horizontalContainer == null)
                    {
                        Debug.LogError("TemplateWindow's horizontalContainer field is not assigned.");
                        return;
                    }
                    GameObject implicitContainer = Instantiate(horizontalContainer, elementParent, false);
                    implicitContainer.name = $"{pendingMinField.Name}_ImplicitContainer";
                    horizontalContainers.Add(implicitContainer);
                    Transform implicitParent = implicitContainer.transform;

                    HorizontalLayoutGroup layoutGroup = implicitContainer.GetComponent<HorizontalLayoutGroup>();


                    string rangeLabel = pendingMinAttr.Label ?? pendingMinField.Name;
                    CreateLabelText(implicitParent, rangeLabel);
                    CreateAndBindRangeSlide(implicitParent, pendingMinField, field, targetObject, pendingMinAttr.Min, pendingMinAttr.Max, pendingMinAttr.Whole, pendingMinAttr.Label);

                    elementCount++;

                    pendingMinField = null;
                    pendingMinAttr = null;
                }
                else
                {
                    Debug.LogWarning($"[RangeSlideMax] on {field.Name} but no matching [RangeSlideMin]. Treating as normal float.");
                }
            }
            else if (field.GetCustomAttribute<SliderUIElement>() is SliderUIElement sliderAttr && 
                     (field.FieldType == typeof(float) || field.FieldType == typeof(int)))
            {
                if (horizontalContainer == null)
                {
                    Debug.LogError("TemplateWindow's horizontalContainer field is not assigned.");
                    return;
                }
                GameObject implicitContainer = Instantiate(horizontalContainer, elementParent, false);
                implicitContainer.name = $"{field.Name}_ImplicitContainer";
                horizontalContainers.Add(implicitContainer);
                Transform implicitParent = implicitContainer.transform;

                HorizontalLayoutGroup layoutGroup = implicitContainer.GetComponent<HorizontalLayoutGroup>();


                string sliderLabel = sliderAttr.Label ?? labelText;
                CreateLabelText(implicitParent, sliderLabel);
                CreateAndBindSlider(implicitParent, field, targetObject, sliderAttr.Min, sliderAttr.Max, sliderAttr.Whole, sliderAttr.Label);
                elementCount++;
            }
            else if (field.FieldType.IsGenericType && field.FieldType.GetGenericTypeDefinition() == typeof(List<>))
            {
                if (horizontalContainer == null)
                {
                    Debug.LogError("TemplateWindow's horizontalContainer field is not assigned.");
                    return;
                }
                GameObject implicitContainer = Instantiate(horizontalContainer, elementParent, false);
                implicitContainer.name = $"{field.Name}_ImplicitContainer";
                horizontalContainers.Add(implicitContainer);
                Transform implicitParent = implicitContainer.transform;

                HorizontalLayoutGroup layoutGroup = implicitContainer.GetComponent<HorizontalLayoutGroup>();


                ListView listView = CreateAndBindListView(implicitParent, field, targetObject);
                if (listView != null)
                {
                    fieldToUIElement[field] = listView;
                    elementCount++;
                }
            }
            else
            {
                if (horizontalContainer == null)
                {
                    Debug.LogError("TemplateWindow's horizontalContainer field is not assigned.");
                    return;
                }
                GameObject implicitContainer = Instantiate(horizontalContainer, elementParent, false);
                implicitContainer.name = $"{field.Name}_ImplicitContainer";
                horizontalContainers.Add(implicitContainer);
                Transform implicitParent = implicitContainer.transform;

                HorizontalLayoutGroup layoutGroup = implicitContainer.GetComponent<HorizontalLayoutGroup>();




                Component uiElement = null;
                if (field.FieldType == typeof(string) || field.FieldType == typeof(int) || field.FieldType == typeof(uint) || field.FieldType == typeof(float)){
					CreateLabelText(implicitParent, labelText);
                    uiElement = CreateAndBindInputField(implicitParent, field, targetObject);
				}
                else if (field.FieldType == typeof(bool)){
                    uiElement = CreateAndBindToggle(implicitParent, field, targetObject);
					CreateLabelText(implicitParent, "  " +labelText);
				}
                else if (field.FieldType.IsEnum){
                    uiElement = CreateAndBindDropdown(implicitParent, field, targetObject);
				}
                else if (field.FieldType == typeof(Vector3)){
					CreateLabelText(implicitParent, labelText);
                    uiElement = CreateAndBindVector3Field(implicitParent, field, targetObject);
				}
                else
                {
                    Debug.LogWarning($"Unsupported field type {field.FieldType} for {field.Name}");
                    Destroy(implicitContainer);
                    continue;
                }

                if (uiElement != null)
                {
                    fieldToUIElement[field] = uiElement;
                    elementCount++;
                }
            }
        }
        else if (member is MethodInfo method && method.ReturnType == typeof(void) && method.GetParameters().Length == 0)
        {
            if (method.GetCustomAttribute<OnWindowEnable>() != null)
                onEnableMethod = method;
            if (method.GetCustomAttribute<OnWindowDisable>() != null)
                onDisableMethod = method;

            RustMapperButton attr = method.GetCustomAttribute<RustMapperButton>();
            if (attr != null)
            {
                string labelText = attr.Label ?? method.Name;
                Button button = CreateAndBindButton(elementParent, method, targetObject, labelText);
                if (button != null)
                {
                    if (explicitHorizontalGroupStack.Count == 0)
                    {
                        contentHeight += button.GetComponent<RectTransform>().sizeDelta.y + contentSpacing;
                        maxContentWidth = Mathf.Max(maxContentWidth, button.GetComponent<RectTransform>().sizeDelta.x);
                    }
                    elementCount++;
                    Debug.Log($"Created Button for {method.Name} under {elementParent.name}");
                }
            }
        }
    }

    // Close any unclosed groups
    while (explicitHorizontalGroupStack.Count > 0)
    {
        GameObject unclosedContainer = explicitHorizontalGroupStack.Pop();
        //unclosedContainer.GetComponent<RectTransform>().sizeDelta = new Vector2(containerWidth, unclosedContainer.GetComponent<RectTransform>().sizeDelta.y);
        elementCount++;
    }

    // Remove empty containers
    foreach (var container in horizontalContainers)
    {
        if (container.transform.childCount == 0)
        {
            Debug.LogWarning($"Removing empty horizontal container: {container.name}");
            contentHeight -= 50 + contentSpacing;
            Destroy(container);
        }
    }

    contentRect.sizeDelta = new Vector2(maxContentWidth, contentHeight);

    LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);

    Debug.Log($"Generated UI with {elementCount} elements for {type.Name}, content width: {maxContentWidth}, height: {contentHeight}");
    EnableTemplates(false);
}

private float CalculateContainerWidth(GameObject container)
{
    float width = -2000f;
    foreach (RectTransform child in container.GetComponentsInChildren<RectTransform>())    {
            width += child.sizeDelta.x;
    }
    return width;
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
	
	// Unity lifecycle: OnEnable for the TemplateWindow GameObject itself
    private void OnEnable()
    {
        Debug.Log("TemplateWindow GameObject enabled.");

        // Invoke the attribute's method if Build has been called and method is cached
        if (targetObject != null && onEnableMethod != null)
        {
            try
            {
                onEnableMethod.Invoke(targetObject, null);
                Debug.Log($"Invoked [OnWindowEnable] method: {onEnableMethod.Name}");
            }
            catch (Exception e)
            {
                Debug.LogError($"Error invoking OnWindowEnable {onEnableMethod.Name}: {e.Message}");
            }
        }
        else if (onEnableMethod != null)
        {
            Debug.LogWarning("[OnWindowEnable] method found but targetObject is null - ensure Build() is called before enabling the GameObject.");
        }
    }

    // Unity lifecycle: OnDisable for the TemplateWindow GameObject itself
    private void OnDisable()
    {
        if (targetObject != null && onDisableMethod != null)
        {
            try
            {
                onDisableMethod.Invoke(targetObject, null);
                Debug.Log($"Invoked [OnWindowDisable] method: {onDisableMethod.Name}");
            }
            catch (Exception e)
            {
                Debug.LogError($"Error invoking OnWindowDisable {onDisableMethod.Name}: {e.Message}");
            }
        }

        Debug.Log("TemplateWindow GameObject disabled.");
    }
	
}