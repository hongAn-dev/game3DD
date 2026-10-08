using NUnit.Framework;
using UnityEngine;

public class ControlTests {

	[Test]
	public void CameraRelativeMoveKeepsMagnitude() {
		Vector3 forward = Vector3.forward, right = Vector3.right;
		Assert.AreEqual(0.25f, BallUserControl.CameraRelative(0.25f, 0f, forward, right).magnitude, 0.001f);
		Assert.LessOrEqual(BallUserControl.CameraRelative(1f, 1f, forward, right).magnitude, 1.0001f);

		// A camera pitched down still yields a horizontal move of the stick's magnitude.
		Vector3 pitched = new Vector3(0f, -0.7f, 0.7f).normalized;
		Vector3 move = BallUserControl.CameraRelative(0f, 0.5f, pitched, right);
		Assert.AreEqual(0f, move.y, 0.0001f);
		Assert.AreEqual(0.5f, move.magnitude, 0.001f);
	}
}
