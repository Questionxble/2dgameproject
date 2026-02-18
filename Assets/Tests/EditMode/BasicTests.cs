using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    public class BasicTests
    {
        [Test]
        public void Unity_IsWorking()
        {
            // Simple test to verify the testing framework is working
            Assert.AreEqual(4, 2 + 2);
        }
        
        [Test]
        public void Unity_CanCreateGameObject()
        {
            // Test Unity object creation
            var go = new GameObject("TestObject");
            Assert.IsNotNull(go);
            Assert.AreEqual("TestObject", go.name);
            Object.DestroyImmediate(go);
        }
        
        [Test]
        public void Game_AssemblyIsAccessible()
        {
            // Test that we can access classes from the Game assembly
            Assert.IsTrue(typeof(PlayerMovement) != null);
            Assert.IsTrue(typeof(GameController) != null);
        }
        
        [Test]
        public void Unity_VectorMath()
        {
            Vector3 a = Vector3.one;
            Vector3 b = Vector3.zero;
            
            Assert.AreEqual(Vector3.one, a);
            Assert.AreEqual(0f, b.magnitude);
            Assert.AreEqual(Mathf.Sqrt(3), a.magnitude, 0.001f);
        }
        
        [Test]
        public void Unity_TransformOperations()
        {
            var go = new GameObject();
            var transform = go.transform;
            
            transform.position = Vector3.right;
            Assert.AreEqual(Vector3.right, transform.position);
            
            transform.localScale = Vector3.one * 2f;
            Assert.AreEqual(Vector3.one * 2f, transform.localScale);
            
            Object.DestroyImmediate(go);
        }
        
        [Test]
        public void Unity_ComponentSystem()
        {
            var go = new GameObject();
            var rigidbody = go.AddComponent<Rigidbody2D>();
            var collider = go.AddComponent<BoxCollider2D>();
            
            Assert.IsNotNull(go.GetComponent<Rigidbody2D>());
            Assert.IsNotNull(go.GetComponent<BoxCollider2D>());
            Assert.AreEqual(rigidbody, go.GetComponent<Rigidbody2D>());
            
            Object.DestroyImmediate(go);
        }
        
        [Test]
        public void Unity_LayerMasks()
        {
            int defaultLayer = LayerMask.NameToLayer("Default");
            LayerMask mask = 1 << defaultLayer;
            
            Assert.IsTrue(mask != 0);
            Assert.AreEqual(defaultLayer, LayerMask.NameToLayer("Default"));
        }
        
        [Test]
        public void Unity_QuaternionMath()
        {
            Quaternion identity = Quaternion.identity;
            Quaternion rotation45 = Quaternion.Euler(0, 0, 45);
            
            Assert.AreEqual(Quaternion.identity, identity);
            Assert.AreNotEqual(identity, rotation45);
        }
        
        [Test]
        public void Unity_TimeSystem()
        {
            // Test that Time system is accessible
            Assert.IsTrue(Time.timeScale >= 0);
            Assert.IsTrue(Time.fixedDeltaTime > 0);
        }
        
        [Test]
        public void Unity_PhysicsConstants()
        {
            Assert.IsTrue(Physics2D.gravity.y < 0); // Gravity pulls down
            Assert.IsTrue(Mathf.Approximately(Mathf.PI, 3.14159f));
        }
    }
}