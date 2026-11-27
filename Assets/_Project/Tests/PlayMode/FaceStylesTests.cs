using System.Linq;
using NUnit.Framework;

namespace Pvz.Runtime.Tests
{
    public class FaceStylesTests
    {
        [Test]
        public void FirstFiveVisitorsUseEveryFeatureAndFirstSixUseEverySkinTone()
        {
            var faces = Enumerable.Range(0, 5).Select(Customer.FaceStyles).ToArray();
            var variants = Enumerable.Range(0, 5).ToArray();
            CollectionAssert.AreEquivalent(variants, faces.Select(f => f.mouth));
            CollectionAssert.AreEquivalent(variants, faces.Select(f => f.nose));
            CollectionAssert.AreEquivalent(variants, faces.Select(f => f.eyes));
            CollectionAssert.AreEquivalent(variants, faces.Select(f => f.ears));
            CollectionAssert.AreEquivalent(Enumerable.Range(0, 6), Enumerable.Range(0, 6).Select(i => Customer.FaceStyles(i).skin));
            Assert.AreNotEqual(Customer.FaceStyles(0), Customer.FaceStyles(5));
        }
    }
}
