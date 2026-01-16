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
    }
}