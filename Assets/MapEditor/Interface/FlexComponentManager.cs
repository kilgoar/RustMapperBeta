using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.IO;
using UnityEngine;
using System.Text;
using UnityEngine.Serialization;
using System.Threading;
using System.Threading.Tasks;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace FC
{
    public static class FlexComponentManager
    {
        private static List<FlexComponent> CurrentFlexComponents = new List<FlexComponent>();
        private static Dictionary<string, Type> ClassDefinitions = new Dictionary<string, Type>();
        private static HashSet<string> LoadedAssemblies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static Dictionary<string, Assembly> TypeToAssemblyMap = new Dictionary<string, Assembly>();
        private static HashSet<string> GeneratedScripts = new HashSet<string>();
        private static HashSet<string> ProcessedTypes = new HashSet<string>();
        private static readonly object scriptLock = new object();
        private static string RustManagedDir = @"E:/rust server/rustds/RustDedicated_Data/managed/";
        private static AssemblyBuilder dynamicAssembly;
        private static ModuleBuilder moduleBuilder;
        private static readonly string DynamicAssemblyName = "FlexComponentDynamicAssembly";
        private static readonly string ScriptOutputDir = @"Assets/MapEditor/Interface/FlexHolders/";

#if UNITY_EDITOR
        [MenuItem("Rust Map Editor/Build Components")]
        public static void BuildAllComponentScripts()
        {
            try
            {
                if (ClassDefinitions.Count == 0)
                {
                    Debug.Log("Initializing FlexComponentManager to load MonoBehaviours.");
                    RuntimeInit();
                }

                if (!Directory.Exists(ScriptOutputDir))
                {
                    Directory.CreateDirectory(ScriptOutputDir);
                    Debug.Log($"Created script output directory: {ScriptOutputDir}");
                }

                int generatedCount = 0;
                Parallel.ForEach(ClassDefinitions, kvp =>
                {
                    string className = kvp.Key;
                    Type type = kvp.Value;
                    if (type.IsSubclassOf(typeof(MonoBehaviour)) && !type.IsAbstract && !type.IsInterface)
                    {
                        GenerateScriptForType(className, type);
                        Interlocked.Increment(ref generatedCount);
                    }
                });

                AssetDatabase.Refresh();
                Debug.Log($"Generated scripts for {generatedCount} MonoBehaviour classes in {ScriptOutputDir}.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error building component scripts: {ex.Message}\nStack: {ex.StackTrace}");
            }
        }
#endif

        public static void RuntimeInit(AssetBundle assetBundle = null, bool minimalLoad = false)
        {
            InitializeDynamicAssembly();
            try
            {
                if (!Directory.Exists(RustManagedDir))
                {
                    Debug.LogError($"Managed directory not found: {RustManagedDir}");
                    return;
                }

                if (!Directory.Exists(ScriptOutputDir))
                {
                    Directory.CreateDirectory(ScriptOutputDir);
                    Debug.Log($"Created script output directory: {ScriptOutputDir}");
                }

                string[] dllPaths = Directory.GetFiles(RustManagedDir, "*.dll", SearchOption.TopDirectoryOnly);
                Debug.Log($"Available DLLs: {string.Join(", ", dllPaths)}");

                foreach (string dllPath in dllPaths)
                {
                    if (!dllPath.EndsWith("Assembly-CSharp.dll", StringComparison.OrdinalIgnoreCase))
                    {
                        LoadAssembly(dllPath);
                    }
                }

                string mainDllPath = Path.Combine(RustManagedDir, "Assembly-CSharp.dll");
                if (!File.Exists(mainDllPath))
                {
                    Debug.LogError($"Main DLL not found: {mainDllPath}");
                    return;
                }

                Assembly mainAssembly = LoadAssembly(mainDllPath);
                if (mainAssembly == null)
                {
                    Debug.LogError("Failed to load Assembly-CSharp.dll");
                    return;
                }

                List<(string, Type)> typesToProcess = new List<(string, Type)>();
                foreach (Type type in mainAssembly.GetTypes())
                {
                    if (type.IsGenericType || !type.IsSubclassOf(typeof(MonoBehaviour)) || type.IsAbstract || type.IsInterface)
                        continue;

                    string typeName = CleanClassName(type.Name);
                    if (!ClassDefinitions.ContainsKey(typeName))
                    {
                        Type targetType = LoadTargetClassWithRetry(mainAssembly, typeName) ?? GenerateProxyClass(typeName, mainAssembly);
                        if (targetType != null)
                        {
                            ClassDefinitions[typeName] = targetType;
                            TypeToAssemblyMap[typeName] = targetType.Assembly;
                            typesToProcess.Add((typeName, targetType));
                        }
                    }
                }

                foreach (var (typeName, targetType) in typesToProcess)
                {
                    PrintClassDetails(typeName);
                    GenerateScriptForType(typeName, targetType);
                }

                Debug.Log($"Loaded {ClassDefinitions.Count} MonoBehaviour class definitions and their dependencies.");
#if UNITY_EDITOR
                AssetDatabase.Refresh();
#endif
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error during RuntimeInit: {ex.Message}\nStack: {ex.StackTrace}");
            }
        }

        private static void InitializeDynamicAssembly()
        {
            try
            {
                AssemblyName assemblyName = new AssemblyName(DynamicAssemblyName);
                dynamicAssembly = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
                moduleBuilder = dynamicAssembly.DefineDynamicModule("MainModule");
                Debug.Log($"Initialized dynamic assembly: {DynamicAssemblyName}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error initializing dynamic assembly: {ex.Message}");
            }
        }

        private static Type GenerateProxyClass(string className, Assembly mainAssembly)
        {
            try
            {
                string cleanClassName = CleanClassName(className);
                Type originalType = mainAssembly.GetType(className, false, true);
                Type baseType = typeof(MonoBehaviour);
                if (originalType != null && originalType.BaseType != null && originalType.BaseType.IsSubclassOf(typeof(MonoBehaviour)) && !originalType.BaseType.IsGenericType)
                {
                    baseType = originalType.BaseType;
                }

                TypeBuilder typeBuilder = moduleBuilder.DefineType(
                    cleanClassName,
                    TypeAttributes.Public | TypeAttributes.Class,
                    baseType
                );

                ConstructorInfo serializeFieldCtor = typeof(SerializeField).GetConstructor(Type.EmptyTypes);

                if (originalType != null)
                {
                    FieldInfo[] fields = originalType.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                    foreach (FieldInfo field in fields)
                    {
                        if (field.IsPrivate || field.IsStatic || !IsSerializableFieldType(field.FieldType)) continue;
                        FieldBuilder fieldBuilder = typeBuilder.DefineField(
                            field.Name,
                            field.FieldType,
                            FieldAttributes.Public
                        );
                        CustomAttributeBuilder attrBuilder = new CustomAttributeBuilder(serializeFieldCtor, new object[0]);
                        fieldBuilder.SetCustomAttribute(attrBuilder);
                        Debug.Log($"Added field {field.Name} of type {field.FieldType.FullName} to proxy class {cleanClassName}");
                    }
                }

                ConstructorBuilder constructor = typeBuilder.DefineConstructor(
                    MethodAttributes.Public,
                    CallingConventions.Standard,
                    Type.EmptyTypes
                );
                ILGenerator constructorIL = constructor.GetILGenerator();
                constructorIL.Emit(OpCodes.Ldarg_0);
                constructorIL.Emit(OpCodes.Call, baseType.GetConstructor(Type.EmptyTypes) ?? typeof(MonoBehaviour).GetConstructor(Type.EmptyTypes));
                constructorIL.Emit(OpCodes.Ret);

                Type proxyType = typeBuilder.CreateType();
                Debug.Log($"Generated proxy class: {cleanClassName}, BaseType: {CleanClassName(baseType.Name)}");
                return proxyType;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error generating proxy class for {className}: {ex.Message}\nStack: {ex.StackTrace}");
                return null;
            }
        }

        private static bool IsSerializableFieldType(Type type)
        {
            if (type.IsGenericType)
            {
                Type genericTypeDef = type.GetGenericTypeDefinition();
                if (genericTypeDef == typeof(List<>))
                {
                    Type[] genericArgs = type.GetGenericArguments();
                    if (genericArgs.Length == 1 && (genericArgs[0] == typeof(string) || genericArgs[0] == typeof(int) || genericArgs[0] == typeof(float)))
                    {
                        return true;
                    }
                }
                Debug.LogWarning($"Generic field type '{type.FullName}' is not supported. Skipping.");
                return false;
            }

            return type == typeof(float) ||
                   type == typeof(int) ||
                   type == typeof(bool) ||
                   type == typeof(string) ||
                   type == typeof(Vector2) ||
                   type == typeof(Vector3) ||
                   type == typeof(Quaternion) ||
                   type == typeof(Color) ||
                   type.IsSubclassOf(typeof(UnityEngine.Object));
        }

        private static string CleanClassName(string className)
        {
            if (string.IsNullOrEmpty(className))
                return className;

            int backtickIndex = className.IndexOf('`');
            if (backtickIndex >= 0)
            {
                string cleanedName = className.Substring(0, backtickIndex);
                Debug.LogWarning($"Generic type suffix detected in class name '{className}'. Cleaned to '{cleanedName}'.");
                return cleanedName;
            }
            return className;
        }

        private static string GetCSharpTypeName(Type type)
        {
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
            {
                Type[] genericArgs = type.GetGenericArguments();
                if (genericArgs.Length == 1)
                {
                    string argTypeName = GetCSharpTypeName(genericArgs[0]);
                    return $"List<{argTypeName}>";
                }
            }

            if (type == typeof(bool)) return "bool";
            if (type == typeof(int)) return "int";
            if (type == typeof(float)) return "float";
            if (type == typeof(string)) return "string";
            if (type == typeof(double)) return "double";
            if (type == typeof(long)) return "long";
            if (type == typeof(short)) return "short";
            if (type == typeof(byte)) return "byte";
            if (type == typeof(char)) return "char";

            if (type == typeof(Vector2)) return "Vector2";
            if (type == typeof(Vector3)) return "Vector3";
            if (type == typeof(Quaternion)) return "Quaternion";
            if (type == typeof(Color)) return "Color";

            if (type.Namespace == "UnityEngine.UI")
            {
                return type.Name;
            }

            if (type.IsSubclassOf(typeof(UnityEngine.Object)) || type.IsSubclassOf(typeof(MonoBehaviour)))
            {
                return CleanClassName(type.Name);
            }

            string typeName = type.FullName?.Replace("+", ".") ?? type.Name;
            return CleanClassName(typeName);
        }

        private static void GenerateScriptForType(string className, Type type)
        {
            lock (scriptLock)
            {
                try
                {
                    string cleanClassName = CleanClassName(className);
                    if (GeneratedScripts.Contains(cleanClassName))
                    {
                        Debug.Log($"Script for {cleanClassName} already generated, skipping.");
                        return;
                    }

                    string scriptPath = Path.Combine(ScriptOutputDir, $"{cleanClassName}.cs");
                    if (File.Exists(scriptPath))
                    {
                        Debug.Log($"Script for {cleanClassName} already exists at {scriptPath}, skipping generation.");
                        GeneratedScripts.Add(cleanClassName);
                        return;
                    }

                    if (type.IsGenericType)
                    {
                        Debug.LogWarning($"Skipping script generation for generic type: {cleanClassName}");
                        return;
                    }

                    StringBuilder script = new StringBuilder();
                    script.AppendLine("using UnityEngine;");

                    HashSet<string> namespaces = new HashSet<string>();

                    Type baseType = type.BaseType;
                    string baseTypeName = "MonoBehaviour";
                    if (baseType != null && baseType != typeof(MonoBehaviour) && baseType.IsSubclassOf(typeof(MonoBehaviour)) && !baseType.IsGenericType)
                    {
                        baseTypeName = GetCSharpTypeName(baseType);
                        if (baseType.Namespace != null && !baseType.Namespace.StartsWith("UnityEngine") && baseType.Namespace != "FC")
                        {
                            if (!string.IsNullOrEmpty(baseType.Namespace))
                            {
                                namespaces.Add(baseType.Namespace);
                            }
                            else
                            {
                                baseTypeName = CleanClassName(baseType.FullName?.Replace("+", ".") ?? baseType.Name);
                            }
                        }
                        if (!ClassDefinitions.ContainsKey(CleanClassName(baseType.Name)) && !baseType.Namespace.StartsWith("UnityEngine"))
                        {
                            GenerateScriptForType(baseType.Name, baseType);
                        }
                    }

                    FieldInfo[] fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                    foreach (FieldInfo field in fields)
                    {
                        if (field.IsPrivate || field.IsStatic || !IsSerializableFieldType(field.FieldType)) continue;

                        if (field.FieldType.Namespace != null && !field.FieldType.Namespace.StartsWith("UnityEngine") && field.FieldType.Namespace != "FC")
                        {
                            if (!string.IsNullOrEmpty(field.FieldType.Namespace))
                            {
                                namespaces.Add(field.FieldType.Namespace);
                            }
                        }
                        else if (field.FieldType.Namespace == "UnityEngine.UI")
                        {
                            namespaces.Add("UnityEngine.UI");
                        }

                        if (field.FieldType.IsSubclassOf(typeof(MonoBehaviour)) || field.FieldType.IsSubclassOf(typeof(UnityEngine.Object)))
                        {
                            string fieldTypeName = CleanClassName(field.FieldType.Name);
                            if (!ClassDefinitions.ContainsKey(fieldTypeName) && !field.FieldType.Namespace.StartsWith("UnityEngine"))
                            {
                                GenerateScriptForType(fieldTypeName, field.FieldType);
                            }
                        }
                    }

                    foreach (string ns in namespaces.OrderBy(n => n))
                    {
                        script.AppendLine($"using {ns};");
                    }

                    if (fields.Any(f => IsSerializableFieldType(f.FieldType) && f.FieldType.IsGenericType && f.FieldType.GetGenericTypeDefinition() == typeof(List<>)))
                    {
                        script.AppendLine("using System.Collections.Generic;");
                    }

                    script.AppendLine();
                    script.AppendLine("namespace FC");
                    script.AppendLine("{");
                    script.AppendLine($"    public class {cleanClassName} : {baseTypeName}");
                    script.AppendLine("    {");

                    foreach (FieldInfo field in fields)
                    {
                        if (field.IsPrivate || field.IsStatic || !IsSerializableFieldType(field.FieldType)) continue;

                        string fieldTypeName = GetCSharpTypeName(field.FieldType);
                        if (field.FieldType.Namespace != null && !field.FieldType.Namespace.StartsWith("UnityEngine") && field.FieldType.Namespace != "FC" && !namespaces.Contains(field.FieldType.Namespace))
                        {
                            fieldTypeName = CleanClassName(field.FieldType.FullName?.Replace("+", ".") ?? field.FieldType.Name);
                        }

                        script.AppendLine($"        [SerializeField]");
                        script.AppendLine($"        public {fieldTypeName} {field.Name};");
                    }

                    script.AppendLine("    }");
                    script.AppendLine("}");

                    File.WriteAllText(scriptPath, script.ToString());
                    GeneratedScripts.Add(cleanClassName);
                    Debug.Log($"Generated script for {cleanClassName} at {scriptPath} with base type {baseTypeName}");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error generating script for {className}: {ex.Message}\nStack: {ex.StackTrace}");
                }
            }
        }

        private static Assembly LoadAssembly(string dllPath)
        {
            try
            {
                string fileName = Path.GetFileNameWithoutExtension(dllPath);
                if (LoadedAssemblies.Contains(dllPath))
                {
                    return Assembly.Load(fileName);
                }

                if (!File.Exists(dllPath))
                {
                    Debug.LogError($"DLL file not found at: {dllPath}");
                    return null;
                }

                FileInfo fileInfo = new FileInfo(dllPath);
                if (fileInfo.Length == 0)
                {
                    Debug.LogError($"DLL file is empty: {dllPath}");
                    return null;
                }

                byte[] dllBytes = File.ReadAllBytes(dllPath);
                if (dllBytes == null || dllBytes.Length == 0)
                {
                    Debug.LogError($"Failed to read bytes from DLL: {dllPath}");
                    return null;
                }

                Assembly assembly = Assembly.Load(dllBytes);
                if (assembly == null)
                {
                    Debug.LogError($"Assembly.Load returned null for: {dllPath}");
                    return null;
                }

                LoadedAssemblies.Add(dllPath);
                Debug.Log($"Loaded assembly: {dllPath}, FullName: {assembly.GetName().FullName}, Version: {assembly.GetName().Version}");
                return assembly;
            }
            catch (BadImageFormatException ex)
            {
                Debug.LogError($"Invalid DLL format for {dllPath}: {ex.Message}");
                return null;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to load DLL {dllPath}: {ex.Message}\nStack: {ex.StackTrace}");
                return null;
            }
        }

        private static Type LoadTargetClassWithRetry(Assembly assembly, string className, int maxRetries = 3)
        {
            string cleanClassName = CleanClassName(className);
            int retryCount = 0;
            while (retryCount < maxRetries)
            {
                try
                {
                    Type targetType = assembly.GetType(className, false, true);
                    if (targetType == null)
                    {
                        Debug.LogWarning($"Class {cleanClassName} not found in {assembly.GetName().Name}");
                        return null;
                    }

                    if (targetType.IsGenericType)
                    {
                        Debug.LogWarning($"Type {cleanClassName} is generic, skipping.");
                        return null;
                    }

                    if (targetType.IsAbstract || targetType.IsInterface)
                    {
                        Debug.LogWarning($"Type {cleanClassName} is abstract or an interface, skipping.");
                        return null;
                    }

                    if (targetType.IsSubclassOf(typeof(MonoBehaviour)) && !ClassDefinitions.ContainsKey(cleanClassName))
                    {
                        ClassDefinitions[cleanClassName] = targetType;
                        Debug.Log($"Cached target class: {cleanClassName}, FullName: {targetType.FullName}, Namespace: {targetType.Namespace}");
                        ResolveDependencies(targetType);
                        GenerateScriptForType(cleanClassName, targetType);
                        return targetType;
                    }
                    else
                    {
                        Debug.LogWarning($"{cleanClassName} is not a MonoBehaviour or already cached.");
                        return null;
                    }
                }
                catch (ReflectionTypeLoadException ex)
                {
                    Debug.LogError($"ReflectionTypeLoadException for {cleanClassName}: {ex.Message}");
                    bool resolved = false;

                    foreach (var loaderEx in ex.LoaderExceptions)
                    {
                        if (loaderEx is FileNotFoundException fnf && fnf.FileName != null)
                        {
                            string missingAssembly = fnf.FileName.Split(',')[0];
                            string assemblyPath = Path.Combine(RustManagedDir, $"{missingAssembly}.dll");
                            Debug.Log($"Attempting to resolve missing assembly: {missingAssembly} at {assemblyPath}");

                            if (File.Exists(assemblyPath))
                            {
                                Assembly loadedAssembly = LoadAssembly(assemblyPath);
                                if (loadedAssembly != null)
                                {
                                    resolved = true;
                                }
                            }
                            else
                            {
                                Debug.LogError($"Missing assembly not found: {assemblyPath}");
                            }
                        }
                        Debug.LogError($"LoaderException: {loaderEx?.Message}");
                    }

                    if (!resolved)
                    {
                        Debug.LogError($"Failed to resolve dependencies for {cleanClassName} after attempt {retryCount + 1}.");
                        return null;
                    }

                    retryCount++;
                    Debug.Log($"Retrying {cleanClassName} after resolving dependencies (Attempt {retryCount + 1}/{maxRetries})");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error loading class {cleanClassName}: {ex.Message}\nStack: {ex.StackTrace}");
                    return null;
                }
            }

            Debug.LogError($"Failed to load {cleanClassName} after {maxRetries} retries.");
            return null;
        }

        private static void ResolveDependencies(Type type)
        {
            if (type == null || type == typeof(MonoBehaviour) || type == typeof(UnityEngine.Object) || type == typeof(object))
                return;

            string cleanTypeName = CleanClassName(type.Name);
            if (ProcessedTypes.Contains(cleanTypeName))
            {
                return;
            }
            ProcessedTypes.Add(cleanTypeName);

            try
            {
                if (type.BaseType != null && !type.BaseType.IsGenericType)
                {
                    ResolveTypeDependency(type.BaseType);
                    if (type.BaseType.IsSubclassOf(typeof(MonoBehaviour)) && !ClassDefinitions.ContainsKey(CleanClassName(type.BaseType.Name)) && !type.BaseType.Namespace.StartsWith("UnityEngine"))
                    {
                        GenerateScriptForType(type.BaseType.Name, type.BaseType);
                    }
                }

                foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
                {
                    if (!field.FieldType.IsGenericType)
                    {
                        ResolveTypeDependency(field.FieldType);
                        if (field.FieldType.IsSubclassOf(typeof(MonoBehaviour)) || field.FieldType.IsSubclassOf(typeof(UnityEngine.Object)))
                        {
                            string fieldTypeName = CleanClassName(field.FieldType.Name);
                            if (!ClassDefinitions.ContainsKey(fieldTypeName) && !field.FieldType.Namespace.StartsWith("UnityEngine"))
                            {
                                GenerateScriptForType(fieldTypeName, field.FieldType);
                            }
                        }
                    }
                }

                foreach (PropertyInfo property in type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
                {
                    if (!property.PropertyType.IsGenericType)
                    {
                        ResolveTypeDependency(property.PropertyType);
                        if (property.PropertyType.IsSubclassOf(typeof(MonoBehaviour)) || property.PropertyType.IsSubclassOf(typeof(UnityEngine.Object)))
                        {
                            string propTypeName = CleanClassName(property.PropertyType.Name);
                            if (!ClassDefinitions.ContainsKey(propTypeName) && !property.PropertyType.Namespace.StartsWith("UnityEngine"))
                            {
                                GenerateScriptForType(propTypeName, property.PropertyType);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error resolving dependencies for {type.FullName}: {ex.Message}\nStack: {ex.StackTrace}");
            }
        }

        private static void ResolveTypeDependency(Type depType)
        {
            if (depType == null || depType.Namespace == null || depType.Assembly == null)
                return;

            if (ClassDefinitions.ContainsKey(CleanClassName(depType.Name)) ||
                depType.Namespace.StartsWith("System") ||
                depType.Namespace.StartsWith("UnityEngine"))
            {
                return;
            }

            try
            {
                string assemblyName = depType.Assembly.GetName().Name;
                string assemblyPath = Path.Combine(RustManagedDir, $"{assemblyName}.dll");
                Debug.Log($"Attempting to resolve dependency for type {depType.FullName} in assembly {assemblyName} at {assemblyPath}");

                if (!LoadedAssemblies.Contains(assemblyPath))
                {
                    if (File.Exists(assemblyPath))
                    {
                        Assembly loadedAssembly = LoadAssembly(assemblyPath);
                        if (loadedAssembly != null)
                        {
                            LoadTargetClassAndDependencies(loadedAssembly, depType.Name);
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"Dependency assembly not found: {assemblyPath}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error resolving dependency for {depType.FullName}: {ex.Message}");
            }
        }

        private static void LoadTargetClassAndDependencies(Assembly assembly, string className)
        {
            try
            {
                string cleanClassName = CleanClassName(className);
                Type targetType = assembly.GetType(className, false, true);
                if (targetType == null)
                {
                    Debug.LogWarning($"Class {cleanClassName} not found in {assembly.GetName().Name}");
                    return;
                }

                if (targetType.IsGenericType)
                {
                    Debug.LogWarning($"Dependency class {cleanClassName} is generic, skipping.");
                    return;
                }

                if (targetType.IsSubclassOf(typeof(MonoBehaviour)) && !ClassDefinitions.ContainsKey(cleanClassName))
                {
                    ClassDefinitions[cleanClassName] = targetType;
                    TypeToAssemblyMap[cleanClassName] = assembly;
                    Debug.Log($"Cached dependency class: {cleanClassName}, FullName: {targetType.FullName}");
                    ResolveDependencies(targetType);
                    GenerateScriptForType(cleanClassName, targetType);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error loading dependency class {CleanClassName(className)}: {ex.Message}\nStack: {ex.StackTrace}");
            }
        }

        private static void PrintClassDetails(string className)
        {
            string cleanClassName = CleanClassName(className);
            if (!ClassDefinitions.TryGetValue(cleanClassName, out Type type))
            {
                Debug.LogWarning($"Class {cleanClassName} not found in ClassDefinitions.");
                return;
            }

            try
            {
                StringBuilder output = new StringBuilder();
                output.AppendLine($"Class Details for {cleanClassName} (Full Name: {type.FullName}, Namespace: {type.Namespace ?? "None"})");

                output.AppendLine("\nFields:");
                FieldInfo[] fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                if (fields.Length == 0)
                {
                    output.AppendLine("  None");
                }
                else
                {
                    foreach (FieldInfo field in fields)
                    {
                        string access = field.IsPublic ? "public" : field.IsPrivate ? "private" : "protected";
                        string staticMod = field.IsStatic ? "static" : "";
                        output.AppendLine($"  {access} {staticMod} {GetCSharpTypeName(field.FieldType)} {field.Name}");
                    }
                }

                Debug.Log(output.ToString());
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error printing class details for {cleanClassName}: {ex.Message}");
            }
        }

        public static void AddTargetComponentClass(string className)
        {
            Debug.LogWarning($"AddTargetComponentClass is deprecated; all MonoBehaviours are automatically processed.");
        }

        public static Type GetClassDefinition(string className)
        {
            return ClassDefinitions.TryGetValue(CleanClassName(className), out Type type) ? type : null;
        }

        public static void RegisterFlexComponent(FlexComponent component)
        {
            if (component != null && !CurrentFlexComponents.Contains(component))
            {
                CurrentFlexComponents.Add(component);
                Debug.Log($"Registered FlexComponent: {component.ComponentType}");
            }
        }

        public static void UnregisterFlexComponent(FlexComponent component)
        {
            CurrentFlexComponents.Remove(component);
            Debug.Log($"Unregistered FlexComponent: {component?.ComponentType}");
        }

        public static List<FlexComponent> GetCurrentFlexComponents()
        {
            return new List<FlexComponent>(CurrentFlexComponents);
        }

        public static void Clear()
        {
            try
            {
                foreach (var component in CurrentFlexComponents)
                {
                    if (component != null && component.gameObject != null)
                    {
                        UnityEngine.Object.Destroy(component.gameObject);
                    }
                }
                CurrentFlexComponents.Clear();
                ClassDefinitions.Clear();
                LoadedAssemblies.Clear();
                TypeToAssemblyMap.Clear();
                GeneratedScripts.Clear();
                ProcessedTypes.Clear();
                dynamicAssembly = null;
                moduleBuilder = null;
                Debug.Log("Cleared FlexComponentManager state.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error clearing FlexComponentManager: {ex.Message}");
            }
        }

        public static bool IsComponentManaged(Component component)
        {
            if (component == null)
            {
                Debug.LogWarning("Component is null.");
                return false;
            }

            string componentTypeName = CleanClassName(component.GetType().Name);
            if (string.IsNullOrEmpty(componentTypeName) || component.GetType().FullName == null)
            {
                Debug.LogWarning($"Component on {component.gameObject.name} has no valid type (missing script).");
                return false;
            }

            if (ClassDefinitions.ContainsKey(componentTypeName))
            {
                Type managedType = ClassDefinitions[componentTypeName];
                if (managedType != null && (component.GetType() == managedType || component.GetType().IsSubclassOf(managedType)))
                {
                    Debug.Log($"Component {componentTypeName} on {component.gameObject.name} is managed.");
                    return true;
                }
            }

            Debug.Log($"Component {componentTypeName} on {component.gameObject.name} is not managed.");
            return false;
        }

        public static Type ResolveMonoBehaviourType(string typeName)
        {
            try
            {
                string cleanTypeName = CleanClassName(typeName);
                if (ClassDefinitions.TryGetValue(cleanTypeName, out Type type))
                {
                    Debug.Log($"Resolved type {cleanTypeName}: {type.FullName}");
                    return type;
                }

                foreach (string assemblyPath in LoadedAssemblies)
                {
                    Assembly assembly = Assembly.Load(Path.GetFileNameWithoutExtension(assemblyPath));
                    if (assembly != null)
                    {
                        foreach (Type t in assembly.GetTypes())
                        {
                            if (t.IsGenericType)
                                continue;
                            if (CleanClassName(t.Name) == cleanTypeName && t.IsSubclassOf(typeof(MonoBehaviour)))
                            {
                                ClassDefinitions[cleanTypeName] = t;
                                TypeToAssemblyMap[cleanTypeName] = assembly;
                                Debug.Log($"Resolved type {cleanTypeName} in assembly {assembly.GetName().Name}: {t.FullName}");
                                GenerateScriptForType(cleanTypeName, t);
                                return t;
                            }
                        }
                    }
                }

                Debug.LogWarning($"Failed to resolve MonoBehaviour type: {cleanTypeName}");
                return null;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error resolving MonoBehaviour type {CleanClassName(typeName)}: {ex.Message}\nStack: {ex.StackTrace}");
                return null;
            }
        }
    }
}