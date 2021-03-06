﻿using FluentAssertions;
using HA4IoT.Actuators.BinaryStateActuators;
using HA4IoT.Contracts.Actuators;
using HA4IoT.Contracts.Components;
using HA4IoT.Tests.Mockups;
using Microsoft.VisualStudio.TestPlatform.UnitTestFramework;

namespace HA4IoT.Actuators.Tests
{
    [TestClass]
    public class LogicalBinaryStateActuatorTests
    {
        [TestMethod]
        public void TurnOn_CombinedActuators()
        {
            var timer = new TestHomeAutomationTimer();
            
            var stateMachineFactory = new TestStateMachineFactory();
            var testActuator1 = stateMachineFactory.CreateTestStateMachineWithOnOffStates();
            var testActuator2 = stateMachineFactory.CreateTestStateMachineWithOnOffStates();
            var testActuator3 = stateMachineFactory.CreateTestStateMachineWithOnOffStates();

            var logicalActautor = new LogicalBinaryStateActuator(ComponentIdFactory.EmptyId, timer);
            logicalActautor.WithActuator(testActuator1);
            logicalActautor.WithActuator(testActuator2);
            logicalActautor.WithActuator(testActuator3);

            logicalActautor.GetState().Equals(BinaryStateId.Off).ShouldBeEquivalentTo(true);

            logicalActautor.SetState(BinaryStateId.On);
            logicalActautor.GetState().Equals(BinaryStateId.On).ShouldBeEquivalentTo(true);

            logicalActautor.SetState(BinaryStateId.Off);
            logicalActautor.GetState().Equals(BinaryStateId.Off).ShouldBeEquivalentTo(true);
        }
    }
}
