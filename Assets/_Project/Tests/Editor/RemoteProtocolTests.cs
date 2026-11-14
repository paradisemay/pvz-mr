using NUnit.Framework;
using Pvz.Runtime.Remote;

namespace Pvz.Editor.Tests
{
    /// <summary>T026: the text protocol of the debug remote.</summary>
    public class RemoteProtocolTests
    {
        [Test]
        public void Commands_AreSplitIntoNameAndArguments()
        {
            Assert.IsTrue(RemoteProtocol.TryParse("  GIVE   BOX-001 ", out var name, out var args));
            Assert.AreEqual("give", name);
            CollectionAssert.AreEqual(new[] { "BOX-001" }, args);
            Assert.IsTrue(RemoteProtocol.TryParse("state", out _, out var none));
            Assert.IsEmpty(none);
            Assert.IsFalse(RemoteProtocol.TryParse("   ", out _, out _));
            Assert.IsFalse(RemoteProtocol.TryParse(null, out _, out _));
        }

        [Test]
        public void Numbers_UseADecimalPoint()
        {
            Assert.IsTrue(RemoteProtocol.TryNumber("0.75", out var value));
            Assert.AreEqual(0.75f, value, 1e-6);
            Assert.IsFalse(RemoteProtocol.TryNumber("abc", out _));
        }

        [Test]
        public void DebugManifest_DeclaresHandTracking_Once()
        {
            const string manifest = "<manifest>\n<application android:label=\"x\"/>\n</manifest>";
            var patched = PvzBuild.DeclareHandTracking(manifest);
            StringAssert.Contains("oculus.software.handtracking\" android:required=\"false\"", patched);
            Assert.Less(patched.IndexOf("handtracking", System.StringComparison.Ordinal),
                patched.IndexOf("<application", System.StringComparison.Ordinal), "before the application element");
            Assert.AreEqual(patched, PvzBuild.DeclareHandTracking(patched), "idempotent");
            Assert.AreEqual("<manifest/>", PvzBuild.DeclareHandTracking("<manifest/>"), "no application: unchanged");
        }

        [Test]
        public void Json_EscapesTexts_AndWritesNumbersWithAPoint()
        {
            var json = new Json().Add("headline", "Заказ №2781\n\"срочно\"").Add("x", 1.5f).Add("ok", true).Add("none", null).ToString();
            Assert.AreEqual("{\"headline\":\"Заказ №2781\\n\\\"срочно\\\"\",\"x\":1.5,\"ok\":true,\"none\":null}", json);
            Assert.AreEqual("{\"ok\":false,\"error\":\"bad\"}", RemoteProtocol.Error("bad"));
            Assert.AreEqual("null", Json.Value(float.NaN));
        }
    }
}
