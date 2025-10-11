using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace FC
{
    public class FlexComponent : MonoBehaviour
    {
        public string ComponentType { get; private set; }
        public Dictionary<string, object> Fields { get; private set; }
        public List<string> Methods { get; private set; }
        private MonoBehaviour target; // Reference to the target MonoBehaviour

        // Build and bind to the target MonoBehaviour
        public void Build(MonoBehaviour targetMonoBehaviour)
        {
            if (targetMonoBehaviour == null)
            {
                Debug.LogError("Target MonoBehaviour is null.");
                return;
            }

            target = targetMonoBehaviour;
            ComponentType = targetMonoBehaviour.GetType().Name;

            // Step 1: Retrieve class definition from manager
            Type targetType = FlexComponentManager.GetClassDefinition(ComponentType);
            if (targetType == null)
            {
                Debug.LogError($"No class definition found for {ComponentType}.");
                return;
            }

            // Step 2: Populate fields and methods
            Fields = new Dictionary<string, object>();
            Methods = new List<string>();

            // Reflect fields
            foreach (FieldInfo field in targetType.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                try
                {
                    Fields[field.Name] = field.GetValue(target);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Failed to get field {field.Name} from {ComponentType}: {ex.Message}");
                }
            }

            // Reflect methods (excluding MonoBehaviour inherited methods and properties)
            foreach (MethodInfo method in targetType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => !m.IsSpecialName && m.DeclaringType != typeof(MonoBehaviour)))
            {
                Methods.Add(method.Name);
            }

            // Register with manager
            FlexComponentManager.RegisterFlexComponent(this);
        }

        // Bind field changes to the target
        public void SetField(string fieldName, object value)
        {
            if (Fields == null || target == null)            {
                Debug.LogWarning("FlexComponent is not initialized or target is missing.");
                return;
            }

            if (Fields.ContainsKey(fieldName))
            {
                Type targetType = FlexComponentManager.GetClassDefinition(ComponentType);
                if (targetType == null)
                {
                    Debug.LogError($"No class definition found for {ComponentType}.");
                    return;
                }

                FieldInfo fieldInfo = targetType.GetField(fieldName, BindingFlags.Public | BindingFlags.Instance);
                if (fieldInfo != null)
                {
                    if (value == null || fieldInfo.FieldType.IsAssignableFrom(value.GetType()))
                    {
                        try
                        {
                            fieldInfo.SetValue(target, value);
                            Fields[fieldName] = value;
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError($"Failed to set field {fieldName} on {ComponentType}: {ex.Message}");
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"Type mismatch for field {fieldName}. Expected {fieldInfo.FieldType}, got {value?.GetType()}");
                    }
                }
                else
                {
                    Debug.LogWarning($"Field {fieldName} not found in {ComponentType}.");
                }
            }
            else
            {
                Debug.LogWarning($"Field {fieldName} not found in FlexComponent.");
            }
        }

        // Unregister on destruction
        private void OnDestroy()
        {
            FlexComponentManager.UnregisterFlexComponent(this);
        }
    }
}