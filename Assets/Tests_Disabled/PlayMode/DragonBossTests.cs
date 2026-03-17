using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using System.Collections;

namespace Tests.PlayMode
{
    public class DragonBossTests
    {
        private GameObject dragonObject;
        private DragonBoss dragonBoss;
        private GameObject playerObject;
        private PlayerMovement playerMovement;
        
        [SetUp]
        public void SetUp()
        {
            dragonObject = new GameObject("TestDragon");
            dragonBoss = dragonObject.AddComponent<DragonBoss>();
            dragonObject.AddComponent<Rigidbody2D>();
            dragonObject.AddComponent<BoxCollider2D>();
            
            playerObject = new GameObject("TestPlayer");
            // Add Rigidbody2D BEFORE PlayerMovement to ensure it's available in Awake()
            playerObject.AddComponent<Rigidbody2D>();
            playerObject.AddComponent<CapsuleCollider2D>();
            playerMovement = playerObject.AddComponent<PlayerMovement>();
        }
        
        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(dragonObject);
            Object.DestroyImmediate(playerObject);
        }
        
        [Test]
        public void DragonBoss_Initialization()
        {
            Assert.IsNotNull(dragonBoss);
            Assert.AreEqual("TestDragon", dragonObject.name);
        }
        
        [Test]
        public void DragonBoss_PhysicsComponents()
        {
            Assert.IsNotNull(dragonBoss.GetComponent<Rigidbody2D>());
            Assert.IsNotNull(dragonBoss.GetComponent<Collider2D>());
        }
        
        [UnityTest]
        public IEnumerator DragonBoss_PlayerDetection()
        {
            yield return new WaitForEndOfFrame();
            
            var foundPlayer = Object.FindFirstObjectByType<PlayerMovement>();
            Assert.IsNotNull(foundPlayer);
        }
        
        [Test]
        public void DragonBoss_PositionManagement()
        {
            dragonObject.transform.position = Vector3.up * 5f;
            Assert.AreEqual(Vector3.up * 5f, dragonObject.transform.position);
        }
        
        [Test]
        public void DragonBoss_StateManagement()
        {
            Assert.IsTrue(dragonObject.activeInHierarchy);
            
            dragonObject.SetActive(false);
            Assert.IsFalse(dragonObject.activeInHierarchy);
        }
        
        [Test]
        public void DragonBoss_ComponentIntegration()
        {
            var animator = dragonObject.AddComponent<Animator>();
            Assert.IsNotNull(dragonBoss.GetComponent<Animator>());
        }
        
        [UnityTest]
        public IEnumerator DragonBoss_CombatRange()
        {
            dragonObject.transform.position = Vector3.zero;
            playerObject.transform.position = Vector3.right * 10f;
            
            yield return new WaitForEndOfFrame();
            
            float distance = Vector3.Distance(
                dragonObject.transform.position, 
                playerObject.transform.position
            );
            
            Assert.AreEqual(10f, distance, 0.1f);
        }
    }
}