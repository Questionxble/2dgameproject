using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using System.Collections;

namespace Tests.PlayMode
{
    public class LadderTests
    {
        private GameObject ladderObject;
        private Ladder ladder;
        private GameObject playerObject;
        
        [SetUp]
        public void SetUp()
        {
            ladderObject = new GameObject("TestLadder");
            ladder = ladderObject.AddComponent<Ladder>();
            ladderObject.AddComponent<BoxCollider2D>().isTrigger = true;
            
            playerObject = new GameObject("TestPlayer");
            playerObject.AddComponent<PlayerMovement>();
            playerObject.AddComponent<Rigidbody2D>();
            playerObject.AddComponent<CapsuleCollider2D>();
        }
        
        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(ladderObject);
            Object.DestroyImmediate(playerObject);
        }
        
        [Test]
        public void Ladder_Initialization()
        {
            Assert.IsNotNull(ladder);
            Assert.AreEqual("TestLadder", ladderObject.name);
        }
        
        [Test]
        public void Ladder_TriggerColliderSetup()
        {
            var collider = ladder.GetComponent<BoxCollider2D>();
            Assert.IsNotNull(collider);
            Assert.IsTrue(collider.isTrigger);
        }
        
        [UnityTest]
        public IEnumerator Ladder_PlayerInteraction()
        {
            yield return new WaitForEndOfFrame();
            
            // Test that player exists for ladder interaction
            var foundPlayer = Object.FindFirstObjectByType<PlayerMovement>();
            Assert.IsNotNull(foundPlayer);
        }
        
        [Test]
        public void Ladder_PositionalAccuracy()
        {
            Vector3 ladderPosition = new Vector3(0f, 5f, 0f);
            ladderObject.transform.position = ladderPosition;
            
            Assert.AreEqual(ladderPosition, ladderObject.transform.position);
        }
        
        [Test]
        public void Ladder_ScaleForClimbing()
        {
            Vector3 ladderScale = new Vector3(1f, 3f, 1f); // Tall ladder
            ladderObject.transform.localScale = ladderScale;
            
            Assert.AreEqual(ladderScale, ladderObject.transform.localScale);
        }
        
        [Test]
        public void Ladder_MultipleLadders()
        {
            var ladder2 = new GameObject("TestLadder2");
            var ladderComponent2 = ladder2.AddComponent<Ladder>();
            
            Assert.AreNotSame(ladder, ladderComponent2);
            Assert.AreNotEqual(ladderObject.name, ladder2.name);
            
            Object.DestroyImmediate(ladder2);
        }
        
        [UnityTest]
        public IEnumerator Ladder_ClimbingZone()
        {
            // Position player near ladder
            playerObject.transform.position = ladderObject.transform.position + Vector3.right * 0.5f;
            
            yield return new WaitForFixedUpdate();
            
            // Test proximity
            float distance = Vector3.Distance(
                playerObject.transform.position, 
                ladderObject.transform.position
            );
            
            Assert.IsTrue(distance < 2f); // Within climbing range
        }
        
        [Test]
        public void Ladder_StateManagement()
        {
            Assert.IsTrue(ladderObject.activeInHierarchy);
            
            ladderObject.SetActive(false);
            Assert.IsFalse(ladderObject.activeInHierarchy);
            
            ladderObject.SetActive(true);
            Assert.IsTrue(ladderObject.activeInHierarchy);
        }
    }
}