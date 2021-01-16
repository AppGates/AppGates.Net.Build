using NUnit.Framework;

namespace AppGates.Net.Build.Tests
{
    public class SmokeTests
    {

        [SetUp]
        public void Setup()
        {
        }


        [Test, Category("CiTest")]
        public void DemoForCiTest()
        {
            Assert.Pass();
        }
        [Test, Category("NoCiTest")]
        public void DemoForCiExcludedTest()
        {
            Assert.Pass();
        }
    }
}