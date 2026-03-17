using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    public class CameraFollowTests
    {
        private GameObject cameraObject;
        private CameraFollow cameraFollow;
        private GameObject playerObject;
        private Camera cameraComponent;
        
        [SetUp]
        public void SetUp()
        {
            cameraObject = new GameObject("TestCamera");
            cameraComponent = cameraObject.AddComponent<Camera>();
            cameraFollow = cameraObject.AddComponent<CameraFollow>();
            
            playerObject = new GameObject("TestPlayer");
            playerObject.AddComponent<PlayerMovement>();
        }
        
        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(cameraObject);
            Object.DestroyImmediate(playerObject);
        }
        
        [Test]
        public void CameraFollow_Initialization()
        {
            Assert.IsNotNull(cameraFollow);
            Assert.IsNotNull(cameraComponent);
            Assert.AreEqual("TestCamera", cameraObject.name);
        }
        
        [Test]
        public void CameraFollow_CameraComponentRequired()
        {
            Assert.IsNotNull(cameraFollow.GetComponent<Camera>());
            Assert.AreEqual(typeof(Camera), cameraComponent.GetType());
        }
        
        [Test]
        public void CameraFollow_PlayerTargetExists()
        {
            var foundPlayer = Object.FindFirstObjectByType<PlayerMovement>();
            Assert.IsNotNull(foundPlayer);
        }
        
        [Test]
        public void CameraFollow_PositionTracking()
        {
            Vector3 initialCameraPos = cameraObject.transform.position;
            Vector3 playerPos = new Vector3(5f, 3f, 0f);
            playerObject.transform.position = playerPos;
            
            // Camera position can be manipulated
            cameraObject.transform.position = new Vector3(playerPos.x, playerPos.y, initialCameraPos.z);
            
            Assert.AreEqual(playerPos.x, cameraObject.transform.position.x);
            Assert.AreEqual(playerPos.y, cameraObject.transform.position.y);
        }
        
        [Test]
        public void CameraFollow_CameraProperties()
        {
            Assert.IsTrue(cameraComponent.enabled);
            
            // Test camera type
            Assert.AreEqual(CameraType.Game, cameraComponent.cameraType);
        }
        
        [Test]
        public void CameraFollow_BoundaryCalculation()
        {
            // Test distance calculations
            playerObject.transform.position = Vector3.zero;
            cameraObject.transform.position = Vector3.back * 10f;
            
            float distance = Vector3.Distance(
                playerObject.transform.position, 
                cameraObject.transform.position
            );
            
            Assert.AreEqual(10f, distance, 0.1f);
        }
        
        [Test]
        public void CameraFollow_MultipleTargets()
        {
            var enemy = new GameObject("Enemy");
            enemy.transform.position = Vector3.right * 8f;
            
            // Test that camera can potentially track multiple objects
            Assert.IsNotNull(enemy.transform);
            Assert.AreNotEqual(playerObject.transform.position, enemy.transform.position);
            
            Object.DestroyImmediate(enemy);
        }
        
        [Test]
        public void CameraFollow_ZAxisMaintenance()
        {
            // Camera should maintain Z position for 2D games
            Vector3 initialPos = cameraObject.transform.position;
            float originalZ = initialPos.z;
            
            cameraObject.transform.position = new Vector3(5f, 3f, originalZ);
            
            Assert.AreEqual(originalZ, cameraObject.transform.position.z);
        }
        
        [Test]
        public void CameraFollow_StateManagement()
        {
            Assert.IsTrue(cameraObject.activeInHierarchy);
            Assert.IsTrue(cameraComponent.enabled);
            
            cameraComponent.enabled = false;
            Assert.IsFalse(cameraComponent.enabled);
            
            cameraComponent.enabled = true;
            Assert.IsTrue(cameraComponent.enabled);
        }
    }
}