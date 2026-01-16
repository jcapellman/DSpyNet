// DSpyNet.Tests/Modules/Test_ModulePersistence.cs
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using DSpyNet.DSPy.Core;
using DSpyNet.DSPy.Modules;
using Xunit;

namespace DSpyNet.Tests.Modules
{
    public class Test_ModulePersistence
    {
        [Fact]
        public async Task Test_SaveAndLoad()
        {
            // Arrange
            var student = new Predict<DSpyNet.Tests.Core.QASignature>();
            student.State.Instruction = "Optimized Instruction";
            student.State.Demos.Add(Example.From(("Question", "Q1"), ("Answer", "A1")));

            var path = "test_module.json";
            if (File.Exists(path)) File.Delete(path);

            try 
            {
                // Act - Save
                await student.SaveAsync(path);

                Assert.True(File.Exists(path));

                // Act - Load into new instance
                var loadedStudent = new Predict<DSpyNet.Tests.Core.QASignature>();
                // Initially default
                Assert.NotEqual("Optimized Instruction", loadedStudent.State.Instruction);
                Assert.Empty(loadedStudent.State.Demos);

                await loadedStudent.LoadAsync(path);

                // Assert
                Assert.Equal("Optimized Instruction", loadedStudent.State.Instruction);
                Assert.Single(loadedStudent.State.Demos);
                Assert.Equal("Q1", loadedStudent.State.Demos[0].Get<string>("Question"));
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }
    }
}