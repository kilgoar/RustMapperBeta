using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
public class ListView : UIBuilder
{
    public Text listName;
    public Toggle collapse;
    public Image expandBackground;
    public Image collapseCheckmark; // Explicit checkmark control
    public GameObject itemLayoutGroup; // Vertical layout for list items
    public GameObject listItems; // Horizontal layout for items (used as a template)
    public Button addButton;
    public object targetList;

    private List<object> cachedListItems = new List<object>();
    private List<GameObject> uiItemGroups = new List<GameObject>();
    private Dictionary<FieldInfo, Component> listItemFieldToUIElement = new Dictionary<FieldInfo, Component>();
    private Type listItemType;
    private bool isInitialized;

    private void Awake()
    {
        if (collapse != null)
        {
            Toggle toggleComponent = collapse.GetComponent<Toggle>();
            if (toggleComponent != null)
            {
                toggleComponent.isOn = false; // Default to expanded
                if (collapseCheckmark != null)
                    collapseCheckmark.gameObject.SetActive(true); // Ensure checkmark is visible initially
                toggleComponent.onValueChanged.AddListener(OnCollapseToggled);
                UpdateCollapseState();
            }
            Debug.Log($"Collapse Toggle initialized: {collapse.name}, isOn: {toggleComponent.isOn}, Checkmark: {collapseCheckmark != null}");
        }
        if (addButton != null)
        {
            addButton.onClick.AddListener(Add);
            Debug.Log($"Add Button assigned: {addButton.name}");
        }
        Debug.Log($"Item Layout Group children count: {itemLayoutGroup.transform.childCount}");
    }

    public void Build(object target)
    {
        if (target == null)
        {
            Debug.LogError("Target list is null. Cannot build ListView.");
            return;
        }

        targetList = target;
        listItemFieldToUIElement.Clear();
        cachedListItems.Clear();

        // Preserve collapse and button, only destroy dynamic items
        List<GameObject> toDestroy = new List<GameObject>();
        foreach (Transform child in itemLayoutGroup.transform)
        {
            if (child.gameObject != collapse.gameObject && child.gameObject != addButton.gameObject)
            {
                if (uiItemGroups.Contains(child.gameObject))
                {
                    toDestroy.Add(child.gameObject);
                    uiItemGroups.Remove(child.gameObject);
                }
            }
        }
        foreach (GameObject item in toDestroy)
        {
            if (item != null)
                Destroy(item);
        }

        // Ensure collapse UI is active if assigned
        if (collapse != null && !collapse.gameObject.activeSelf)
            collapse.gameObject.SetActive(true);
        if (expandBackground != null && !expandBackground.gameObject.activeSelf)
            expandBackground.gameObject.SetActive(true);
        if (collapseCheckmark != null && !collapseCheckmark.gameObject.activeSelf)
            collapseCheckmark.gameObject.SetActive(true);

        // Determine list type and build
        Type listType = target.GetType();
        if (!listType.IsGenericType || listType.GetGenericTypeDefinition() != typeof(List<>))
        {
            Debug.LogError($"Target object {listType.Name} is not a List<T>.");
            return;
        }

        listItemType = listType.GetGenericArguments()[0];
        if (listName != null) listName.text = listType.Name; // Ensure listName is set

        IEnumerable list = target as IEnumerable;
        cachedListItems.Clear();
        foreach (object item in list)
            cachedListItems.Add(item ?? Activator.CreateInstance(listItemType));

        if (cachedListItems.Count == 0)
        {
            Debug.Log("List is empty. No items to display.");
            UpdateCollapseState();
            return;
        }

        foreach (object item in cachedListItems)
            CreateListItemUI(item);

        isInitialized = true;
        if (collapse != null) collapse.GetComponent<Toggle>().isOn = false; // Force expanded
        UpdateCollapseState();
    }

    private void CreateListItemUI(object item)
    {
        if (listItems == null || item == null)
        {
            Debug.LogError("ListItems or item is null. Cannot create UI.");
            return;
        }

        GameObject itemGroup = Instantiate(listItems, itemLayoutGroup.transform, false);
        uiItemGroups.Add(itemGroup);
        Debug.Log($"Created item group: {itemGroup.name} for item: {item}");

        Type itemType = item.GetType();
        var fields = itemType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .OrderBy(f => f.MetadataToken);

        foreach (FieldInfo field in fields)
        {
            RustMapperUIElement attr = field.GetCustomAttribute<RustMapperUIElement>();
            string labelText = attr?.Label ?? field.Name;

            bool isLabel = field.FieldType == typeof(string) && (field.IsInitOnly || field.IsLiteral || field.GetCustomAttribute<System.Runtime.CompilerServices.IsReadOnlyAttribute>() != null);

            Transform elementParent = itemGroup.transform;

            if (isLabel)
            {
                Text newText = CreateLabelText(elementParent, (string)field.GetValue(item) ?? labelText);
                if (newText != null)
                    listItemFieldToUIElement[field] = newText;
            }
            else
            {
                // CreateLabelText(elementParent, labelText); // Skip for now
                Component uiElement = null;
                if (field.FieldType == typeof(string) || field.FieldType == typeof(int) || field.FieldType == typeof(uint) || field.FieldType == typeof(float))
                    uiElement = CreateAndBindInputField(elementParent, field, item);
                else if (field.FieldType == typeof(bool))
                    uiElement = CreateAndBindToggle(elementParent, field, item);
                else if (field.FieldType.IsEnum)
                    uiElement = CreateAndBindDropdown(elementParent, field, item);
                else if (field.FieldType == typeof(Vector3))
                    uiElement = CreateAndBindVector3Field(elementParent, field, item);
                else
                {
                    Debug.LogWarning($"Unsupported field type {field.FieldType} for field {field.Name}");
                    continue;
                }

                if (uiElement != null)
                    listItemFieldToUIElement[field] = uiElement;
            }
        }
    }

    public void Add()
    {
        if (targetList == null || listItemType == null)
        {
            Debug.LogError("Cannot add item: targetList or listItemType is not initialized.");
            return;
        }

        object newItem = Activator.CreateInstance(listItemType);
        MethodInfo addMethod = targetList.GetType().GetMethod("Add");
        if (addMethod == null)
        {
            Debug.LogError("Cannot find Add method on target list.");
            return;
        }
        addMethod.Invoke(targetList, new object[] { newItem });

        cachedListItems.Add(newItem);
        CreateListItemUI(newItem);
        UpdateCollapseState();
    }

    public void OnCollapseToggled(bool isCollapsed)
    {
        if (collapse != null)
        {
            Toggle toggleComponent = collapse.GetComponent<Toggle>();
            if (toggleComponent != null)
                toggleComponent.isOn = isCollapsed;
        }
        UpdateCollapseState();
    }

    private void UpdateCollapseState()
    {
        if (collapse == null) return;
        Toggle toggleComponent = collapse.GetComponent<Toggle>();
        bool isCollapsed = toggleComponent.isOn;
        Debug.Log($"UpdateCollapseState: isCollapsed={isCollapsed}, uiItemGroups count={uiItemGroups.Count}");

        foreach (GameObject itemGroup in uiItemGroups)
        {
            if (itemGroup != null)
                itemGroup.SetActive(!isCollapsed);
        }
        if (expandBackground != null)
            expandBackground.gameObject.SetActive(!isCollapsed);
        if (collapseCheckmark != null)
            collapseCheckmark.gameObject.SetActive(isCollapsed);
        Debug.Log($"Checkmark active: {collapseCheckmark != null && collapseCheckmark.gameObject.activeSelf}");
    }


    private void Update()
    {
        if (!isInitialized || targetList == null) return;

        foreach (var pair in listItemFieldToUIElement)
        {
            FieldInfo field = pair.Key;
            Component uiElement = pair.Value;
            object item = cachedListItems.FirstOrDefault(i => field.DeclaringType == i.GetType());

            if (item == null) continue;

            try
            {
                if (uiElement is Text text && field.FieldType == typeof(string) && (field.IsInitOnly || field.IsLiteral || field.GetCustomAttribute<System.Runtime.CompilerServices.IsReadOnlyAttribute>() != null))
                {
                    string currentValue = (string)field.GetValue(item) ?? "";
                    if (text.text != currentValue)
                        text.text = currentValue;
                }
                else if (uiElement is InputField inputField)
                {
                    string currentValue = null;
                    if (field.FieldType == typeof(string))
                        currentValue = (string)field.GetValue(item) ?? "";
                    else if (field.FieldType == typeof(int))
                        currentValue = ((int)field.GetValue(item)).ToString();
                    else if (field.FieldType == typeof(uint))
                        currentValue = ((uint)field.GetValue(item)).ToString();
                    else if (field.FieldType == typeof(float))
                        currentValue = ((float)field.GetValue(item)).ToString("F2");

                    if (currentValue != null && inputField.text != currentValue)
                        inputField.text = currentValue;
                }
                else if (uiElement is Toggle toggle && field.FieldType == typeof(bool))
                {
                    bool currentValue = (bool)field.GetValue(item);
                    if (toggle.isOn != currentValue)
                        toggle.isOn = currentValue;
                }
                else if (uiElement is Dropdown dropdown && field.FieldType.IsEnum)
                {
                    object currentValue = field.GetValue(item);
                    string[] enumNames = Enum.GetNames(field.FieldType);
                    int index = Array.IndexOf(enumNames, currentValue.ToString());
                    if (index >= 0 && dropdown.value != index)
                        dropdown.value = index;
                }
                else if (uiElement is Vector3Field vector3Field && field.FieldType == typeof(Vector3))
                {
                    Vector3 currentValue = (Vector3)field.GetValue(item);
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
}