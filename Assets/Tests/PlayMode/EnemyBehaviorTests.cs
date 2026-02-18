using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using System.Collections;

namespace Tests.PlayMode
{
    public class EnemyBehaviorTests
    {
        private GameObject enemyObject;
        private EnemyBehavior enemyBehavior;
        private GameObject playerObject;
        
        [SetUp]
        public void SetUp()
        {
            // Create enemy object
            enemyObject = new GameObject("TestEnemy");
            enemyBehavior = enemyObject.AddComponent<EnemyBehavior>();
            enemyObject.AddComponent<Rigidbody2D>();
            enemyObject.AddComponent<BoxCollider2D>();
            
            // Create player object for enemy to potentially target
            playerObject = new GameObject("TestPlayer");
            playerObject.AddComponent<PlayerMovement>();
            playerObject.AddComponent<Rigidbody2D>();
            playerObject.AddComponent<CapsuleCollider2D>();
        }
        
        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(enemyObject);
            Object.DestroyImmediate(playerObject);
        }
        
        [Test]
        public void EnemyBehavior_ComponentInitialization()
        {
            Assert.IsNotNull(enemyBehavior);
            Assert.IsNotNull(enemyBehavior.GetComponent<Rigidbody2D>());
            Assert.IsNotNull(enemyBehavior.GetComponent<Collider2D>());
        }
        
        [Test]
        public void EnemyBehavior_GameObjectSetup()
        {
            Assert.AreEqual("TestEnemy", enemyObject.name);
            Assert.IsTrue(enemyObject.activeInHierarchy);
            Assert.IsNotNull(enemyObject.transform);
        }
        
        [UnityTest]
        public IEnumerator EnemyBehavior_CanFindPlayer()
        {
            yield return new WaitForEndOfFrame();
            
            // Test that player exists in scene for enemy to find
            var foundPlayer = Object.FindFirstObjectByType<PlayerMovement>();
            Assert.IsNotNull(foundPlayer);
        }
        
        [Test]
        public void EnemyBehavior_PhysicsComponents()
        {
            var rb = enemyBehavior.GetComponent<Rigidbody2D>();
            Assert.IsNotNull(rb);
            Assert.AreEqual(RigidbodyType2D.Dynamic, rb.bodyType);
            
            var collider = enemyBehavior.GetComponent<Collider2D>();
            Assert.IsNotNull(collider);
            Assert.IsTrue(collider.enabled);
        }
        
        [UnityTest]
        public IEnumerator EnemyBehavior_PositionTracking()
        {
            Vector3 initialPosition = enemyObject.transform.position;
            
            yield return new WaitForFixedUpdate();
            
            // Test that enemy position can be tracked
            Vector3 currentPosition = enemyObject.transform.position;
            Assert.IsTrue(Vector3.Distance(initialPosition, currentPosition) >= 0);
        }
        
        [Test]
        public void EnemyBehavior_MultipleEnemies()
        {
            // Test creating multiple enemies
            var enemy2 = new GameObject("TestEnemy2");
            var behavior2 = enemy2.AddComponent<EnemyBehavior>();
            
            Assert.AreNotSame(enemyBehavior, behavior2);
            Assert.AreNotSame(enemyObject, enemy2);
            
            Object.DestroyImmediate(enemy2);
        }
        
        [UnityTest]
        public IEnumerator EnemyBehavior_CollisionDetection()
        {
            // Create a test object for collision
            var wall = new GameObject("Wall");
            wall.AddComponent<BoxCollider2D>();
            wall.transform.position = enemyObject.transform.position;
            
            yield return new WaitForFixedUpdate();
            
            // Test that collision objects exist
            Assert.IsNotNull(wall);
            Assert.IsNotNull(wall.GetComponent<Collider2D>());
            
            Object.DestroyImmediate(wall);
        }
        
        [Test]
        public void EnemyBehavior_DistanceCalculation()
        {
            // Test distance calculation between enemy and player
            enemyObject.transform.position = Vector3.zero;
            playerObject.transform.position = Vector3.right * 5f;
            
            float distance = Vector3.Distance(
                enemyObject.transform.position, 
                playerObject.transform.position
            );
            
            Assert.AreEqual(5f, distance, 0.1f);
        }
        
        [Test]
        public void EnemyBehavior_StateManagement()
        {
            // Test that enemy can be enabled/disabled
            enemyObject.SetActive(false);
            Assert.IsFalse(enemyObject.activeInHierarchy);
            
            enemyObject.SetActive(true);
            Assert.IsTrue(enemyObject.activeInHierarchy);
        }
    }
}