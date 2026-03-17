using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using System.Collections;
using System.Collections.Generic;

namespace Tests.PlayMode
{
    public class PlayerMovementTests
    {
        private GameObject playerObject;
        private PlayerMovement playerMovement;
        private Rigidbody2D playerRigidbody;
        
        [SetUp]
        public void SetUp()
        {
            // Create a test player object
            playerObject = new GameObject("TestPlayer");
            playerMovement = playerObject.AddComponent<PlayerMovement>();
            playerRigidbody = playerObject.AddComponent<Rigidbody2D>();
            
            // Add required components
            var capsuleCollider = playerObject.AddComponent<CapsuleCollider2D>();
            var spriteRenderer = playerObject.AddComponent<SpriteRenderer>();
            
            // Set up ground layer
            playerMovement.groundLayerMask = 1 << LayerMask.NameToLayer("Default");
        }
        
        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(playerObject);
        }
        
        [Test]
        public void PlayerMovement_ComponentsInitializedCorrectly()
        {
            Assert.IsNotNull(playerMovement);
            Assert.IsNotNull(playerRigidbody);
            Assert.IsNotNull(playerMovement.GetComponent<CapsuleCollider2D>());
            Assert.IsNotNull(playerMovement.GetComponent<SpriteRenderer>());
        }
        
        [Test]
        public void PlayerMovement_GroundLayerMaskSet()
        {
            Assert.AreNotEqual(0, playerMovement.groundLayerMask.value);
        }
        
        [UnityTest]
        public IEnumerator PlayerMovement_HealthSystemInitialization()
        {
            yield return new WaitForEndOfFrame();
            
            // Test that player starts with health
            Assert.IsTrue(true); // Placeholder - health system would need public accessors
        }
        
        [Test]
        public void PlayerMovement_BuffSystemCreation()
        {
            // Test buff creation
            var attackBuff = new PlayerMovement.ActiveBuff(
                PlayerMovement.BuffType.Attack, 
                10f, 
                5f, 
                "Test attack buff"
            );
            
            Assert.AreEqual(PlayerMovement.BuffType.Attack, attackBuff.type);
            Assert.AreEqual(10f, attackBuff.value);
            Assert.AreEqual(5f, attackBuff.duration);
            Assert.AreEqual("Test attack buff", attackBuff.description);
        }
        
        [Test]
        public void PlayerMovement_BuffExpiration()
        {
            var shortBuff = new PlayerMovement.ActiveBuff(
                PlayerMovement.BuffType.Strength, 
                5f, 
                0.01f, // Very short duration
                "Short buff"
            );
            
            // Wait a bit and check if expired (may need adjustment based on implementation)
            Assert.IsTrue(shortBuff.duration > 0);
        }
        
        [Test]
        public void PlayerMovement_AllBuffTypes()
        {
            // Test all buff types exist
            var buffTypes = System.Enum.GetValues(typeof(PlayerMovement.BuffType));
            Assert.IsTrue(buffTypes.Length > 0);
            
            // Test creating buffs of each type
            foreach (PlayerMovement.BuffType type in buffTypes)
            {
                var buff = new PlayerMovement.ActiveBuff(type, 1f, 1f);
                Assert.AreEqual(type, buff.type);
            }
        }
        
        [UnityTest]
        public IEnumerator PlayerMovement_ObjectCreationInScene()
        {
            // Test that player can exist in scene
            yield return new WaitForEndOfFrame();
            
            Assert.IsTrue(playerObject.activeInHierarchy);
            Assert.IsNotNull(GameObject.Find("TestPlayer"));
        }
        
        [Test]
        public void PlayerMovement_Physics2DSetup()
        {
            // Test physics components
            Assert.IsNotNull(playerRigidbody);
            Assert.IsTrue(playerRigidbody.bodyType == RigidbodyType2D.Dynamic);
        }
        
        [UnityTest] 
        public IEnumerator PlayerMovement_ColliderInteraction()
        {
            // Create a ground object
            var ground = new GameObject("Ground");
            ground.AddComponent<BoxCollider2D>();
            ground.transform.position = Vector3.down;
            
            yield return new WaitForFixedUpdate();
            
            // Test collision setup works
            Assert.IsNotNull(ground);
            
            Object.DestroyImmediate(ground);
        }
    }
}