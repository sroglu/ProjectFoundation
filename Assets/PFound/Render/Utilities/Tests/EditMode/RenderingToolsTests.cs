using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using PFound.Render.Utilities;

namespace PFound.Render.Utilities.Tests
{
    public sealed class RenderingToolsTests
    {
        static Material NewStandardMaterial()
        {
            var shader = Shader.Find("Standard");
            if (shader == null)
                Assert.Ignore("Standard shader not available in this test environment.");
            return new Material(shader);
        }

        [Test]
        public void SetMaterialFade_UsesSrcAlphaBlendAndTransparentQueue()
        {
            var material = NewStandardMaterial();
            try
            {
                RenderingTools.SetMaterialFade(material);

                Assert.AreEqual((int)BlendMode.SrcAlpha, material.GetInt("_SrcBlend"));
                Assert.AreEqual((int)BlendMode.OneMinusSrcAlpha, material.GetInt("_DstBlend"));
                Assert.AreEqual(0, material.GetInt("_ZWrite"));
                Assert.AreEqual((int)RenderQueue.Transparent, material.renderQueue);
                Assert.IsTrue(material.IsKeywordEnabled("_ALPHABLEND_ON"));
                Assert.IsFalse(material.IsKeywordEnabled("_ALPHAPREMULTIPLY_ON"));
            }
            finally { Object.DestroyImmediate(material); }
        }

        [Test]
        public void SetMaterialTransparent_UsesPremultipliedBlend()
        {
            var material = NewStandardMaterial();
            try
            {
                RenderingTools.SetMaterialTransparent(material);

                Assert.AreEqual((int)BlendMode.One, material.GetInt("_SrcBlend"));
                Assert.AreEqual((int)BlendMode.OneMinusSrcAlpha, material.GetInt("_DstBlend"));
                Assert.AreEqual((int)RenderQueue.Transparent, material.renderQueue);
                Assert.IsTrue(material.IsKeywordEnabled("_ALPHAPREMULTIPLY_ON"));
                Assert.IsFalse(material.IsKeywordEnabled("_ALPHABLEND_ON"));
            }
            finally { Object.DestroyImmediate(material); }
        }

        [Test]
        public void TintMaterial_MultipliesMainColor()
        {
            var material = NewStandardMaterial();
            try
            {
                material.SetColor("_Color", new Color(0.8f, 0.8f, 0.8f, 1f));
                RenderingTools.TintMaterial(material, new Color(0.5f, 1f, 0.25f, 1f));

                var c = material.GetColor("_Color");
                Assert.AreEqual(0.4f, c.r, 0.001f);
                Assert.AreEqual(0.8f, c.g, 0.001f);
                Assert.AreEqual(0.2f, c.b, 0.001f);
            }
            finally { Object.DestroyImmediate(material); }
        }

        [Test]
        public void SetCameraScissor_ClampsRectAndSkewsProjection()
        {
            var go = new GameObject("scissor-cam");
            try
            {
                var camera = go.AddComponent<Camera>();
                camera.orthographic = true;
                camera.ResetProjectionMatrix();
                var unscissored = camera.projectionMatrix;

                // Request a rect that overhangs the top-right; expect it clamped.
                RenderingTools.SetCameraScissor(camera, new Rect(0.5f, 0.5f, 0.75f, 0.75f));

                Assert.AreEqual(0.5f, camera.rect.width, 0.001f);
                Assert.AreEqual(0.5f, camera.rect.height, 0.001f);
                Assert.AreNotEqual(unscissored, camera.projectionMatrix);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void SetSharedMaterials_AssignsArrayToRenderer()
        {
            var go = new GameObject("renderer-host");
            var a = NewStandardMaterial();
            var b = NewStandardMaterial();
            try
            {
                var renderer = go.AddComponent<MeshRenderer>();
                RenderingTools.SetSharedMaterials(renderer, a, b);

                var assigned = renderer.sharedMaterials;
                Assert.AreEqual(2, assigned.Length);
                Assert.AreSame(a, assigned[0]);
                Assert.AreSame(b, assigned[1]);
            }
            finally
            {
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(a);
                Object.DestroyImmediate(b);
            }
        }
    }
}
