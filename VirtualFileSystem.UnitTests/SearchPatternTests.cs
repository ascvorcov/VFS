using NUnit.Framework;

using VirtualFileSystem.Utilities;


namespace VirtualFileSystem.UnitTests
{
    [TestFixture]
    public class SearchPatternTests
    {
        [Test]
        public void BasicTest()
        {
            Assert.IsTrue(new SearchPattern("*").Match("pattern"));
            Assert.IsTrue(new SearchPattern("?").Match("p"));
            Assert.IsTrue(new SearchPattern("??").Match("pa"));
            Assert.IsTrue(new SearchPattern("??t").Match("pat"));
            Assert.IsTrue(new SearchPattern("p*te?n").Match("pattern"));
            Assert.IsTrue(new SearchPattern("*te?n").Match("pattern"));
            Assert.IsTrue(new SearchPattern("****n").Match("pattern"));
            Assert.IsTrue(new SearchPattern("*ab?e").Match("zabcdabce"));
        }
    }
}