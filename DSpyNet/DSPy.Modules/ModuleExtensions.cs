// DSpyNet/DSPy.Modules/ModuleExtensions.cs
using System.Collections.Generic;
using System.Reflection;

namespace DSpyNet.DSPy.Modules
{
    public static class ModuleExtensions
    {
        /// <summary>
        /// Recursively finds all IPredictor instances within a Module.
        /// Used by optimizers to tune instructions for all steps in a pipeline.
        /// </summary>
        public static List<IPredictor> NamedPredictors(this Module module)
        {
            var predictors = new List<IPredictor>();
            Traverse(module, predictors, new HashSet<object>());
            return predictors;
        }

        /// <summary>
        /// Like <see cref="NamedPredictors"/> but also returns the field path that produced each predictor.
        /// Auto-property names are unwrapped (e.g. "&lt;Urgency&gt;k__BackingField" -> "Urgency").
        /// Used by GEPA to identify predictors stably across iterations (instructions change, names don't).
        /// </summary>
        public static List<(string Name, IPredictor Predictor)> NamedPredictorsWithNames(this Module module)
        {
            var named = new List<(string, IPredictor)>();
            TraverseNamed(module, string.Empty, named, new HashSet<object>());
            return named;
        }

        private static void Traverse(object current, List<IPredictor> predictors, HashSet<object> visited)
        {
            if (current == null) return;
            if (visited.Contains(current)) return;
            visited.Add(current);

            // If it is a predictor, add it
            if (current is IPredictor predictor)
            {
                predictors.Add(predictor);
            }

            // Scan fields and properties for nested Modules or Predictors
            // We scan fields because private backing fields often hold the instances
            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            var fields = current.GetType().GetFields(flags);
            foreach (var field in fields)
            {
                // Avoid recursion into system types
                if (field.FieldType.IsPrimitive || field.FieldType == typeof(string)) continue;

                if (typeof(Module).IsAssignableFrom(field.FieldType) ||
                    typeof(IPredictor).IsAssignableFrom(field.FieldType))
                {
                    var value = field.GetValue(current);
                    if (value != null)
                    {
                        Traverse(value, predictors, visited);
                    }
                }
                // Handle Lists/Arrays of Modules if needed (skipped for brevity, assuming standard composition)
            }
        }

        private static void TraverseNamed(object current, string path,
            List<(string, IPredictor)> predictors, HashSet<object> visited)
        {
            if (current == null) return;
            if (visited.Contains(current)) return;
            visited.Add(current);

            if (current is IPredictor predictor)
            {
                predictors.Add((string.IsNullOrEmpty(path) ? current.GetType().Name : path, predictor));
            }

            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var fields = current.GetType().GetFields(flags);
            foreach (var field in fields)
            {
                if (field.FieldType.IsPrimitive || field.FieldType == typeof(string)) continue;

                if (typeof(Module).IsAssignableFrom(field.FieldType) ||
                    typeof(IPredictor).IsAssignableFrom(field.FieldType))
                {
                    var value = field.GetValue(current);
                    if (value == null) continue;

                    var childName = CleanFieldName(field.Name);
                    var childPath = string.IsNullOrEmpty(path) ? childName : $"{path}.{childName}";
                    TraverseNamed(value, childPath, predictors, visited);
                }
            }
        }

        // "<Urgency>k__BackingField" -> "Urgency"; plain fields pass through.
        private static string CleanFieldName(string raw)
        {
            if (raw.StartsWith("<"))
            {
                var end = raw.IndexOf('>');
                if (end > 1) return raw.Substring(1, end - 1);
            }
            return raw;
        }
    }
}