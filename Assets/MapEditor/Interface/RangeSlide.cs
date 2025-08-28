using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Events;

public class RangeSlide : MonoBehaviour
{
    public SliderFieldSO sfData; 
    public InputField lowField, highField;
    public Slider lowSlider, highSlider;
    public Text titleText;

    // Single UnityEvent for when either slider handle is released
    public UnityEvent onSliderReleased;

    private bool isWhole;

    public void Awake()
    {
        Init();
        AddSliderReleaseEvents();
    }

    public void Init()
    {
        Setup();
        Configure();
    }

    // Setup references
    public void Setup()
    {
        titleText = GetComponentInChildren<Text>();
    }

    public void Configure()
    {
        lowSlider.minValue = sfData.minSetting;
        highSlider.minValue = sfData.minSetting;

        lowSlider.maxValue = sfData.maxSetting;
        highSlider.maxValue = sfData.maxSetting;

        titleText.text = sfData.title;
        lowSlider.wholeNumbers = sfData.whole;
        highSlider.wholeNumbers = sfData.whole;

        lowField.characterLimit = 6;
        highField.characterLimit = 6;

        UpdateFieldsFromSliders();

        lowField.onEndEdit.AddListener(_ => FieldChanged());
        highField.onEndEdit.AddListener(_ => FieldChanged());
        lowSlider.onValueChanged.AddListener(_ => SliderChanged());
        highSlider.onValueChanged.AddListener(_ => SliderChanged());
    }

    private void FieldChanged()
    {
        if (float.TryParse(lowField.text, out float lowValue) && float.TryParse(highField.text, out float highValue))
        {
            lowValue = Mathf.Clamp(lowValue, lowSlider.minValue, highSlider.value);
            highValue = Mathf.Clamp(highValue, lowSlider.value, highSlider.maxValue);

            lowSlider.value = lowValue;
            highSlider.value = highValue;
        }
    }

    private void SliderChanged()
    {
        lowSlider.value = Mathf.Clamp(lowSlider.value, lowSlider.minValue, highSlider.value);
        highSlider.value = Mathf.Clamp(highSlider.value, lowSlider.value, highSlider.maxValue);

        UpdateFieldsFromSliders();
    }

    private void UpdateFieldsFromSliders()
    {
        if (sfData.whole)
        {
            lowField.SetTextWithoutNotify(lowSlider.value.ToString());
            highField.SetTextWithoutNotify(highSlider.value.ToString());
        }
        else
        {
            lowField.SetTextWithoutNotify(lowSlider.value.ToString("F3"));
            highField.SetTextWithoutNotify(highSlider.value.ToString("F3"));
        }
    }

    private void OnEnable()
    {
        UpdateFieldsFromSliders();
    }
    
    private void OnValidate()
    {
        Init();
    }

    // Method to add PointerUp event listeners to both sliders
    private void AddSliderReleaseEvents()
    {
        // Add event for lowSlider
        EventTrigger lowTrigger = lowSlider.gameObject.GetComponent<EventTrigger>();
        if (lowTrigger == null)
        {
            lowTrigger = lowSlider.gameObject.AddComponent<EventTrigger>();
        }

        EventTrigger.Entry lowEntry = new EventTrigger.Entry
        {
            eventID = EventTriggerType.PointerUp
        };
        lowEntry.callback.AddListener((eventData) => { OnSliderHandleReleased(); });
        lowTrigger.triggers.Add(lowEntry);

        // Add event for highSlider
        EventTrigger highTrigger = highSlider.gameObject.GetComponent<EventTrigger>();
        if (highTrigger == null)
        {
            highTrigger = highSlider.gameObject.AddComponent<EventTrigger>();
        }

        EventTrigger.Entry highEntry = new EventTrigger.Entry
        {
            eventID = EventTriggerType.PointerUp
        };
        highEntry.callback.AddListener((eventData) => { OnSliderHandleReleased(); });
        highTrigger.triggers.Add(highEntry);
    }

    // Method called when either slider handle is released
    public void OnSliderHandleReleased()
    {
        onSliderReleased?.Invoke();
    }
}