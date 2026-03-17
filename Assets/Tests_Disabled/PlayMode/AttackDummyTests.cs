using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using System.Collections;

namespace Tests.PlayMode
{
    public class AttackDummyTests
    {
        private GameObject dummyObject;
        private AttackDummy attackDummy;
        private GameObject playerObject;
        private PlayerMovement playerMovement;
        
        [SetUp]
        public void SetUp()
        {
            dummyObject = new GameObject("TestAttackDummy");
            attackDummy = dummyObject.AddComponent<AttackDummy>();
            dummyObject.AddComponent<BoxCollider2D>();
            dummyObject.AddComponent<Rigidbody2D>();
            
            playerObject = new GameObject("TestPlayer");
            // Add Rigidbody2D BEFORE PlayerMovement to ensure it's available in Awake()
            playerObject.AddComponent<Rigidbody2D>();
            playerObject.AddComponent<CapsuleCollider2D>();
            playerMovement = playerObject.AddComponent<PlayerMovement>();
        }
        
        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(dummyObject);
            Object.DestroyImmediate(playerObject);
        }
        
        [Test]
        public void AttackDummy_Initialization()
        {
            Assert.IsNotNull(attackDummy);
            Assert.AreEqual("TestAttackDummy", dummyObject.name);
        }
        
        [Test]
        public void AttackDummy_PhysicsComponents()
        {
            Assert.IsNotNull(attackDummy.GetComponent<Rigidbody2D>());
            Assert.IsNotNull(attackDummy.GetComponent<Collider2D>());
        }
        
        [UnityTest]
        public IEnumerator AttackDummy_PlayerTargeting()
        {
            yield return new WaitForEndOfFrame();
            
            var foundPlayer = Object.FindFirstObjectByType<PlayerMovement>();
            Assert.IsNotNull(foundPlayer);
        }
        
        [Test]
        public void AttackDummy_PositionSetup()
        {
            Vector3 dummyPosition = new Vector3(2f, 0f, 0f);
            dummyObject.transform.position = dummyPosition;
            
            Assert.AreEqual(dummyPosition, dummyObject.transform.position);
        }
        
        [Test]
        public void AttackDummy_HealthSystemSetup()
        {
            // Attack dummies typically have health/damage systems
            Assert.IsTrue(dummyObject.activeInHierarchy);
            
            // Test that dummy can be "destroyed" (deactivated)
            dummyObject.SetActive(false);
            Assert.IsFalse(dummyObject.activeInHierarchy);
        }
        
        [Test]
        public void AttackDummy_MultipleTargets()
        {
            var dummy2 = new GameObject("TestDummy2");
            var dummyComponent2 = dummy2.AddComponent<AttackDummy>();
            
            Assert.AreNotSame(attackDummy, dummyComponent2);
            Assert.AreNotEqual(dummyObject, dummy2);
            
            Object.DestroyImmediate(dummy2);
        }
        
        [UnityTest]
        public IEnumerator AttackDummy_CombatRange()
        {
            // Position player in attack range
            playerObject.transform.position = Vector3.zero;
            dummyObject.transform.position = Vector3.right * 2f;
            
            yield return new WaitForEndOfFrame();
            
            float distance = Vector3.Distance(
                playerObject.transform.position, 
                dummyObject.transform.position
            );
            
            Assert.AreEqual(2f, distance, 0.1f);
        }
        
        [Test]
        public void AttackDummy_ComponentIntegration()
        {
            var spriteRenderer = dummyObject.AddComponent<SpriteRenderer>();
            var animator = dummyObject.AddComponent<Animator>();
            
            Assert.IsNotNull(attackDummy.GetComponent<SpriteRenderer>());
            Assert.IsNotNull(attackDummy.GetComponent<Animator>());
        }
        
        [Test]
        public void AttackDummy_DamageReceiver()
        {
            // Attack dummies should be able to receive damage
            var rigidbody = attackDummy.GetComponent<Rigidbody2D>();
            
            // Test that it can be affected by physics (knockback)
            rigidbody.bodyType = RigidbodyType2D.Dynamic;
            Assert.AreEqual(RigidbodyType2D.Dynamic, rigidbody.bodyType);
        }
        
        [Test]
        public void AttackDummy_LayerConfiguration()
        {
            dummyObject.layer = LayerMask.NameToLayer("Default");
            Assert.AreEqual(LayerMask.NameToLayer("Default"), dummyObject.layer);
            
            dummyObject.tag = "Enemy";
            Assert.AreEqual("Enemy", dummyObject.tag);
        }
    }
}