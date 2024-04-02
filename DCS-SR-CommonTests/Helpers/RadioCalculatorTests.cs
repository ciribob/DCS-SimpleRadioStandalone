using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Ciribob.DCS.SimpleRadio.Standalone.Common.DCSState;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Tests.RadioCalculatorTests
{
    [TestClass()]
    public class TestFreqencyToWaveLength
    {
        //Frequency to Wavelength tests
        //These are generally for verifying that our constants dont change.
        [TestMethod()]
        public void ConstantVerification1Hz()
        {
            // 1 hz should return the speed of light constant.
            double wavelength = RadioCalculator.FrequencyToWaveLength(1);
        
            Assert.AreEqual(299792458, wavelength);
        }
    
        [TestMethod()]
        public void ConstantVerification1MHz()
        {
            double wavelength = RadioCalculator.FrequencyToWaveLength(1000000);
        
            Assert.AreEqual(299.792458, wavelength);
        }
    
        [TestMethod()]
        public void ConstantVerification1GHz()
        {
            // 1 hz should return the speed of light constant.
            double wavelength = RadioCalculator.FrequencyToWaveLength(1000000000);
        
            Assert.AreEqual(0.299792458, wavelength);
        }
    
        [TestMethod()]
        public void ConstantVerification123MHz()
        {
            // 1 hz should return the speed of light constant.
            double wavelength = RadioCalculator.FrequencyToWaveLength(123000000);
        
            Assert.AreEqual(2.437337056910569, wavelength);
        }
    }

    /*
    [TestClass()]
    public class TestFriisTransmissionReceivedPower
    {
        // Not implemented due to 0 usages at time of writing.
    }
    */
    
    [TestClass()]
    public class TestFriisMaximumTransmissionRange
    {
        [TestMethod()]
        public void ConstantVerification1Hz()
        {
            // Rounded to 8 decimal places because of Pi being used.
            double range = Math.Round(RadioCalculator.FriisMaximumTransmissionRange(1), 8);
            Assert.AreEqual(94975336055448.6, range);
        }
        [TestMethod()]
        public void ConstantVerification1MHz()
        {
            // Rounded to 8 decimal places because of Pi being used.
            double range = Math.Round(RadioCalculator.FriisMaximumTransmissionRange(1000000), 8);
            Assert.AreEqual(94975336.0554486, range);
        }
        [TestMethod()]
        public void ConstantVerification1GHz()
        {
            // Rounded to 8 decimal places because of Pi being used.
            double range = Math.Round(RadioCalculator.FriisMaximumTransmissionRange(1000000000), 8);
            Assert.AreEqual(94975.33605545, range);
        }
        [TestMethod()]
        public void ConstantVerification123MHz()
        {
            // Rounded to 8 decimal places because of Pi being used.
            double range = Math.Round(RadioCalculator.FriisMaximumTransmissionRange(123000000), 8);
            Assert.AreEqual(772157.20370283, range);
        }
    }

    [TestClass()]
    public class TestCanHearTransmission
    {
        [TestMethod()]
        public void OneKilometerAt1MHz()
        {
            bool canHear = RadioCalculator.CanHearTransmission(1000, 1000000);
            
            Assert.IsTrue(canHear);
        }
        [TestMethod()]
        public void OneHundredKilometersAt1MHz()
        {
            bool canHear = RadioCalculator.CanHearTransmission(100000, 1000000);
            
            Assert.IsTrue(canHear);
        }
        
        [TestMethod()]
        public void OneMillionKilometersAt123MHz()
        {
            bool canHear = RadioCalculator.CanHearTransmission(1000000, 1000000);
            
            Assert.IsTrue(canHear);
        }
        
        [TestMethod()]
        public void ExactCheckAt123MHz()
        {
            bool canHear = RadioCalculator.CanHearTransmission(772158, 123000000);
            
            Assert.IsFalse(canHear);
        }
    }


    [TestClass()]
    public class TestCalculateDistanceHaversine
    {
        [TestMethod()]
        public void DoubleInstantiatedButNeverDefined()
        {
            var position = new DCSLatLngPosition();
            double distance = RadioCalculator.CalculateDistanceHaversine(position, position);

            Assert.AreEqual(0, distance);
        }
        
        [TestMethod()]
        public void SingleInstantiatedButNeverDefined()
        {
            var positionA = new DCSLatLngPosition();
            var positionB = new DCSLatLngPosition(100, 100, 100);
            double distance = RadioCalculator.CalculateDistanceHaversine(positionA, positionB);

            // Really not sure how it should handle this.
            Assert.Inconclusive("Possibly needs error handling");
        }

        [TestMethod()]
        public void SameLocationCheck()
        {
            // The transmission is coming from inside the jet... *horror movie sting*
            var position = new DCSLatLngPosition(100, 100, 100);
            double roundedDistance = RadioCalculator.CalculateDistanceHaversine(position, position);

            Assert.AreEqual(0, roundedDistance);
        }
        
        [TestMethod()]
        public void KnownDistanceCheck()
        {
            // The transmission is coming from inside the jet... *horror movie sting*
            var positionA = new DCSLatLngPosition(100, 100, 100);
            var positionB = new DCSLatLngPosition(101, 101, 101);
            double distance = Math.Round(RadioCalculator.CalculateDistanceHaversine(positionA, positionB), 8);

            Assert.AreEqual(113022.10863318, distance);
        }
    }
}