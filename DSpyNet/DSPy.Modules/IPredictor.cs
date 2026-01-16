// DSpyNet/DSPy.Modules/IPredictor.cs
using DSpyNet.DSPy.Core;

namespace DSpyNet.DSPy.Modules
{
    /// <summary>
    /// Non-generic interface to access Predictor state via Reflection.
    /// Required for Optimizers like COPRO and MIPRO.
    /// </summary>
    public interface IPredictor
    {
        SignatureState State { get; }
    }
}