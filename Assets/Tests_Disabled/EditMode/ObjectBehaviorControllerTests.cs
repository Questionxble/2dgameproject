using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    public class ObjectBehaviorControllerTests
    {
        private GameObject behaviorObject;
        private ObjectBehaviorController behaviorController;
        
        [SetUp]
        public void SetUp()
        {
            behaviorObject = new GameObject("TestBehaviorObject");
            behaviorController = behaviorObject.AddComponent<ObjectBehaviorController>();
        }
        
        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(behaviorObject);
        }
        
        [Test]
        public void ObjectBehaviorController_Initialization()
        {
            Assert.IsNotNull(behaviorController);
            Assert.AreEqual("TestBehaviorObject", behaviorObject.name);
        }
        
        [Test]
        public void ObjectBehaviorController_ComponentSetup()
        {
            Assert.IsNotNull(behaviorController.GetComponent<ObjectBehaviorController>());
            Assert.AreEqual(typeof(ObjectBehaviorController), behaviorController.GetType());
        }
        
        [Test]
        public void ObjectBehaviorController_TransformManipulation()
        {
            Vector3 testPosition = new Vector3(3f, 4f, 0f);
            behaviorObject.transform.position = testPosition;
            
            Assert.AreEqual(testPosition, behaviorObject.transform.position);
        }
        
        [Test]
        public void ObjectBehaviorController_StateControl()
        {
            Assert.IsTrue(behaviorObject.activeInHierarchy);
            
            behaviorObject.SetActive(false);
            Assert.IsFalse(behaviorObject.activeInHierarchy);
            
            behaviorObject.SetActive(true);
            Assert.IsTrue(behaviorObject.activeInHierarchy);
        }
        
        [Test]
        public void ObjectBehaviorController_ComponentIntegration()
        {
            var rigidbody = behaviorObject.AddComponent<Rigidbody2D>();
            var collider = behaviorObject.AddComponent<BoxCollider2D>();
            
            Assert.IsNotNull(behaviorController.GetComponent<Rigidbody2D>());
            Assert.IsNotNull(behaviorController.GetComponent<BoxCollider2D>());
        }
        
        [Test]
        public void ObjectBehaviorController_MultipleInstances()
        {
            var object2 = new GameObject("TestBehavior2");
            var controller2 = object2.AddComponent<ObjectBehaviorController>();
            
            Assert.AreNotSame(behaviorController, controller2);
            Assert.AreNotEqual(behaviorObject, object2);
            
            Object.DestroyImmediate(object2);
        }
        
        [Test]
        public void ObjectBehaviorController_HierarchyOperations()
        {
            var parent = new GameObject("Parent");
            behaviorObject.transform.SetParent(parent.transform);
            
            Assert.AreEqual(parent.transform, behaviorObject.transform.parent);
            Assert.AreEqual(1, parent.transform.childCount);
            
            Object.DestroyImmediate(parent);
        }
        
        [Test]
        public void ObjectBehaviorController_TagAndLayerManagement()
        {
            behaviorObject.tag = "Behavior";
            Assert.AreEqual("Behavior", behaviorObject.tag);
            
            behaviorObject.layer = LayerMask.NameToLayer("Default");
            Assert.AreEqual(LayerMask.NameToLayer("Default"), behaviorObject.layer);
        }
        
        [Test]
        public void ObjectBehaviorController_ScaleManipulation()
        {
            Vector3 testScale = new Vector3(2f, 2f, 1f);
            behaviorObject.transform.localScale = testScale;
            
            Assert.AreEqual(testScale, behaviorObject.transform.localScale);
        }
        
        [Test]
        public void ObjectBehaviorController_RotationControl()
        {
            Quaternion testRotation = Quaternion.Euler(0, 0, 45f);
            behaviorObject.transform.rotation = testRotation;
            
            Assert.AreEqual(testRotation, behaviorObject.transform.rotation);
        }
    }
}