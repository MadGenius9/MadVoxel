using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
// Deliberately no `using UnityEngine.Rendering.Universal`, and no SRP Core types by
// name either - Volume and VolumeProfile live in a package, not in base Unity, so
// naming them would break the reference-assembly compile check the same way.

namespace MadVoxel.Core
{
    /// <summary>
    /// Sets up the post stack: tonemapping, grading, bloom and a vignette.
    ///
    /// This is most of the difference between a render and a photograph. Untonemapped
    /// output clips every bright thing to flat white and leaves the shadows muddy,
    /// which is why an engine's default look reads as "a game" no matter how good the
    /// models are. It costs nothing to fix and needs no art.
    ///
    /// <b>Through reflection, deliberately.</b> Every other file in this project
    /// avoids naming a URP type - materials are found by shader name and the pipeline
    /// is detected through <see cref="GraphicsSettings"/> - which is what lets the
    /// whole codebase be compiled and checked against plain Unity reference
    /// assemblies outside the editor. One <c>using UnityEngine.Rendering.Universal</c>
    /// would end that for the sake of a dozen lines that run once at startup.
    ///
    /// So it asks the runtime for the types instead, and if they are not there it
    /// quietly does nothing. The worst case is a game that looks exactly as it does
    /// today, which is the same bargain the AO shader makes - and for the same reason,
    /// since this project has already shipped one render-pipeline failure that turned
    /// the whole world magenta.
    /// </summary>
    public static class PostProcessing
    {
        const string UrpNamespace = "UnityEngine.Rendering.Universal.";
        const string CoreNamespace = "UnityEngine.Rendering.";

        /// <summary>Whether the stack was actually installed. False is not an error.</summary>
        public static bool Installed { get; private set; }

        /// <summary>Why it did not install, for the console. Empty when it did.</summary>
        public static string Reason { get; private set; }

        static GameObject _installed;
        static ScriptableObject _profile;

        /// <summary>
        /// Drops the stack. Called before installing another, so a world reloaded in
        /// place does not leave a volume and a profile behind each time - and a
        /// profile is a ScriptableObject, which nothing else will ever collect.
        /// </summary>
        public static void Remove()
        {
            if (_installed != null) UnityEngine.Object.Destroy(_installed);
            if (_profile != null) UnityEngine.Object.Destroy(_profile);

            _installed = null;
            _profile = null;
            Installed = false;
        }

        public static void Install(Transform parent, Camera camera)
        {
            Remove();
            Reason = "";

            if (camera == null) { Reason = "no camera"; return; }

            // HDR first, and it matters on its own: bloom and tonemapping both need
            // values above 1 to work with, and without it a bright sky is already
            // clipped before anything gets a chance to roll it off.
            camera.allowHDR = true;

            if (GraphicsSettings.currentRenderPipeline == null)
            {
                Reason = "no scriptable pipeline - built-in has no volume stack";
                return;
            }

            try
            {
                if (!EnableOnCamera(camera)) return;
                if (!BuildVolume(parent)) return;

                Installed = true;
            }
            catch (Exception e)
            {
                // Reflection against a package that has moved. Not worth taking the
                // game down for: the picture is merely plainer. Tear down whatever was
                // half-built, so a failure does not leave an inert volume behind.
                Reason = e.GetType().Name + ": " + e.Message;
                Remove();
            }
        }

        /// <summary>Turns post-processing on for the camera. URP ignores volumes otherwise.</summary>
        static bool EnableOnCamera(Camera camera)
        {
            var dataType = FindUrpType("UniversalAdditionalCameraData");
            if (dataType == null) { Reason = "UniversalAdditionalCameraData not found"; return false; }

            var data = camera.GetComponent(dataType) ?? camera.gameObject.AddComponent(dataType);
            if (data == null) { Reason = "could not attach camera data"; return false; }

            var flag = dataType.GetProperty("renderPostProcessing",
                BindingFlags.Public | BindingFlags.Instance);
            if (flag == null) { Reason = "renderPostProcessing not found"; return false; }

            flag.SetValue(data, true);
            return true;
        }

        static bool BuildVolume(Transform parent)
        {
            var volumeType = FindType(CoreNamespace + "Volume");
            var profileType = FindType(CoreNamespace + "VolumeProfile");

            if (volumeType == null || profileType == null)
            {
                Reason = "the volume framework is not present";
                return false;
            }

            var go = new GameObject("PostProcessing");
            if (parent != null) go.transform.SetParent(parent, false);
            _installed = go;

            var volume = go.AddComponent(volumeType);
            if (volume == null) { Reason = "could not attach a volume"; Remove(); return false; }

            SetProperty(volume, "isGlobal", true);
            SetProperty(volume, "priority", 0f);

            var profile = ScriptableObject.CreateInstance(profileType);
            if (profile == null) { Reason = "could not create a profile"; Remove(); return false; }

            _profile = profile;
            profile.name = "MadVoxel Post";
            SetProperty(volume, "sharedProfile", profile);

            // Tonemapping is the one that matters. ACES rolls the highlights off
            // instead of clipping them, which is what stops a bright sky reading as a
            // hole cut in the screen.
            Add(profile, "Tonemapping", component =>
            {
                SetEnum(component, "mode", 2);   // Neutral = 1, ACES = 2
            });

            // A touch of contrast and warmth. Restrained on purpose: this is a grading
            // pass nobody has looked at, and the failure mode of a bold one is a game
            // that looks broken rather than merely plain.
            Add(profile, "ColorAdjustments", component =>
            {
                SetFloat(component, "postExposure", 0.15f);
                SetFloat(component, "contrast", 12f);
                SetFloat(component, "saturation", 6f);
            });

            Add(profile, "Bloom", component =>
            {
                SetFloat(component, "threshold", 1.05f);
                SetFloat(component, "intensity", 0.45f);
                SetFloat(component, "scatter", 0.62f);
            });

            // Barely there. A vignette you notice is a vignette that is too strong,
            // and this one is doing nothing but pulling the eye off the screen edges.
            Add(profile, "Vignette", component =>
            {
                SetFloat(component, "intensity", 0.22f);
                SetFloat(component, "smoothness", 0.5f);
            });

            return true;
        }

        // ---------------------------------------------------------------- plumbing

        /// <summary>
        /// Finds a type by full name across everything loaded.
        ///
        /// Swept rather than looked up by assembly-qualified name, because these have
        /// moved between packages and been renamed more than once, and a hard-coded
        /// assembly name is a thing that silently stops matching after an upgrade.
        /// </summary>
        static Type FindType(string fullName)
        {
            var loaded = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < loaded.Length; i++)
            {
                var found = loaded[i].GetType(fullName, false);
                if (found != null) return found;
            }
            return null;
        }

        static Type FindUrpType(string name)
        {
            return FindType(UrpNamespace + name);
        }

        static void SetProperty(object target, string name, object value)
        {
            if (target == null) return;

            var property = target.GetType().GetProperty(name,
                BindingFlags.Public | BindingFlags.Instance);
            if (property != null && property.CanWrite) { property.SetValue(target, value); return; }

            var field = target.GetType().GetField(name, BindingFlags.Public | BindingFlags.Instance);
            if (field != null) field.SetValue(target, value);
        }

        /// <summary>
        /// Adds one override to the profile and configures it.
        ///
        /// A missing component is skipped rather than fatal: losing the bloom is a
        /// smaller price than losing the tonemapping because the two were installed
        /// together.
        /// </summary>
        static void Add(object profile, string typeName, Action<object> configure)
        {
            var type = FindUrpType(typeName);
            if (type == null) return;

            // VolumeProfile.Add(Type, bool) - the overload that takes a type rather
            // than a generic parameter, which is the only one reachable from here.
            var add = profile.GetType().GetMethod("Add",
                BindingFlags.Public | BindingFlags.Instance, null,
                new[] { typeof(Type), typeof(bool) }, null);
            if (add == null) return;

            var component = add.Invoke(profile, new object[] { type, true });
            if (component == null) return;

            configure(component);
        }

        /// <summary>
        /// Sets a volume parameter and marks it overridden.
        ///
        /// Both halves are needed. A parameter with a value and no override state is
        /// ignored by the stack entirely, which looks exactly like the effect not
        /// being there and is the single easiest way to spend an afternoon.
        /// </summary>
        static void SetFloat(object component, string field, float value)
        {
            var parameter = component.GetType().GetField(field,
                BindingFlags.Public | BindingFlags.Instance);
            if (parameter == null) return;

            var holder = parameter.GetValue(component);
            if (holder == null) return;

            var valueField = holder.GetType().GetProperty("value",
                BindingFlags.Public | BindingFlags.Instance);

            if (valueField != null && valueField.CanWrite) valueField.SetValue(holder, value);
            Override(holder);
        }

        /// <summary>
        /// Marks a parameter overridden.
        ///
        /// <c>overrideState</c> is a property on VolumeParameter, not a field, and
        /// asking for it as a field simply returns null - so the earlier version of
        /// this never marked anything and worked only because Add(type, true) had
        /// already set every override on the way in. Something that works by accident
        /// stops working the moment the accident does.
        /// </summary>
        static void Override(object parameter)
        {
            if (parameter == null) return;

            var property = parameter.GetType().GetProperty("overrideState",
                BindingFlags.Public | BindingFlags.Instance);
            if (property != null && property.CanWrite) { property.SetValue(parameter, true); return; }

            var field = parameter.GetType().GetField("overrideState",
                BindingFlags.Public | BindingFlags.Instance);
            if (field != null) field.SetValue(parameter, true);
        }

        static void SetEnum(object component, string field, int value)
        {
            var parameter = component.GetType().GetField(field,
                BindingFlags.Public | BindingFlags.Instance);
            if (parameter == null) return;

            var holder = parameter.GetValue(component);
            if (holder == null) return;

            var valueProperty = holder.GetType().GetProperty("value",
                BindingFlags.Public | BindingFlags.Instance);

            if (valueProperty != null && valueProperty.CanWrite)
            {
                valueProperty.SetValue(holder, Enum.ToObject(valueProperty.PropertyType, value));
            }
            Override(holder);
        }
    }
}
