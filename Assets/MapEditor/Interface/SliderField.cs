using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class SliderField : MonoBehaviour
{
    public SliderFieldSO sfData;
    
    public InputField field;
    public Slider slider;
    public Text text;

    // UnityEvent to trigger when the slider handle is released
    public UnityEvent onSliderReleased;

    public void Awake()
    {
        Init();
        AddSliderReleaseEvent();
    }
    
    public void Init()
    {
        Setup();
        Configure();
    }
    
    public void Setup()
    {
        field = GetComponentInChildren<InputField>();
        slider = GetComponentInChildren<Slider>();
        text = GetComponentInChildren<Text>();
    }
    
    public void Configure()
    {
        slider.minValue = sfData.minSetting;
        slider.maxValue = sfData.maxSetting;
        text.text = sfData.title;
        slider.wholeNumbers = sfData.whole;
        field.contentType = InputField.ContentType.DecimalNumber;
        field.characterLimit = 6;
        if (sfData.whole)
        {
            field.contentType = InputField.ContentType.IntegerNumber;
        }
    }
    
    private void OnValidate()
    {
        Init();
    }
    
    public void FieldChanged()
    {
        slider.value = float.Parse(field.text);
    }
    
    public void SliderChanged()
    {
        if (!sfData.whole)
        {
            field.text = slider.value.ToString("F3");
            return;
        }
        field.text = slider.value.ToString();
    }

    // Method to add the PointerUp event listener to the slider
    private void AddSliderReleaseEvent()
    {
        EventTrigger trigger = slider.gameObject.GetComponent<EventTrigger>();
        if (trigger == null)
        {
            trigger = slider.gameObject.AddComponent<EventTrigger>();
        }

        EventTrigger.Entry entry = new EventTrigger.Entry
        {
            eventID = EventTriggerType.PointerUp
        };
        entry.callback.AddListener((eventData) => { OnSliderHandleReleased(); });

        trigger.triggers.Add(entry);
    }

    // Method that gets called when the slider handle is released
    public void OnSliderHandleReleased()
    {
        onSliderReleased?.Invoke();
    }
}