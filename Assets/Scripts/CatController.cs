﻿using UnityEngine;
using System.Collections;

public class CatController : MonoBehaviour {
	public float catSpeed = 10.0f;
	public float leapSpeed = 20.0f;
	public float catJumpForce = 100.0f;
	public float closeEnoughToTarget = 0.5f;
	public float whiskerLength = 0.8f;
	public GameObject catGoal;
	public float catPowForce = 100.0f;
	public float jumpLength = 1.0f;
	public float minJumpHeight = 0.2f;
	public float maxJumpHeight = 2.0f;
	public float adjacentMoveMax = 1.0f;
	public AudioClip[] meows;
	public AudioClip purr;
	public bool moving;
	public bool betweenLevels = false;


	Animator anim;
	Vector3 targetMovePoint;
	Vector3 targetWayPoint;
	Quaternion targetRotation;
	Rigidbody rb;
	Collider col;
	bool jumping;
	float jumpHeightTester;
	int ignoreClickTesterLayer;
	int alsoIgnoreMouseClickOnly;
	Collider previousDestZone = null;
	AudioSource asource;
	FlyAround murderFly = null;
	RunAround murderRat = null;

	// Use this for initialization
	void Start () {
		targetMovePoint = transform.position;
		targetRotation = transform.rotation;
		rb = GetComponent<Rigidbody> ();
		col = GetComponent<Collider> ();
		asource = GetComponent<AudioSource> ();
		jumping = false; 
		moving = false;
		jumpHeightTester = 4.0f;
		anim = GetComponentInChildren<Animator> ();
		ignoreClickTesterLayer = ~LayerMask.GetMask ("ClickTester", "Ignore Raycast", "CamIgnore");
		alsoIgnoreMouseClickOnly = ~LayerMask.GetMask ("ClickTester", "Ignore Raycast", "MouseClickOnly", "CamIgnore");
	}

	void MouseTarget(){
		Ray camRay = Camera.main.ScreenPointToRay (Input.mousePosition);
		RaycastHit floorHit;
		if (Physics.Raycast (camRay, out floorHit, 5000.0f, ignoreClickTesterLayer)) {

//			targetMovePoint = floorHit.point;

			int clickLayer = floorHit.collider.gameObject.layer;
			if (clickLayer == LayerMask.NameToLayer("MouseClickOnly")){
				Vector3 vectDif = floorHit.collider.transform.position - transform.position;
				float vectLen = vectDif.magnitude;
				bool adjHop = vectLen < adjacentMoveMax;
				if(previousDestZone != null){
					AdjacentMovement amScript = floorHit.collider.GetComponent<AdjacentMovement>();
					if(amScript && amScript.colliderMatch(floorHit.collider)){
						adjHop = true;
					}
				}
				RaycastHit jumpableTester;
				if(adjHop || Physics.Raycast(transform.position, vectDif,out jumpableTester, vectLen, alsoIgnoreMouseClickOnly) == false){
//					Debug.Log("vectLen: " + vectLen);
					Transform goalPos = floorHit.collider.transform.GetChild(0);
					targetMovePoint = goalPos.position;
					targetRotation = Quaternion.LookRotation(goalPos.transform.forward);
					catGoal.transform.position = goalPos.position;
					moving = true;
					previousDestZone = floorHit.collider;
					if(adjHop){
						StartJump(goalPos.position);
					}
				} else {
					if(adjHop){
						MessageManager.instance.PostMessage("You want me to jump where?");
					}else{
						MessageManager.instance.PostMessage("I can't jump that far!");
//						Debug.Log ("View blocked by: " + jumpableTester.collider.name);
					}
				}

			} else if(clickLayer == LayerMask.NameToLayer("Interactable")){

				float distToToy = Vector3.Distance (transform.position, floorHit.collider.transform.position);
				FlyAround faScript = floorHit.collider.GetComponent<FlyAround>();
				RunAround raScript = floorHit.collider.GetComponent<RunAround>();
				if(raScript) {
					murderRat = raScript;
					targetMovePoint = raScript.transform.position;
					targetRotation = transform.rotation;
					catGoal.transform.position = raScript.transform.position;
					moving = true;
					previousDestZone = floorHit.collider;
					StartJump(raScript.transform.position);
					ScoreManager.AddPoints(200);
				} else if(faScript) {
					murderFly = faScript;
					targetMovePoint = faScript.transform.position;
					targetRotation = transform.rotation;
					catGoal.transform.position = faScript.transform.position;
					moving = true;
					previousDestZone = floorHit.collider;
					StartJump(faScript.transform.position);
					ScoreManager.AddPoints(200);
				} else if(distToToy < whiskerLength){
					ToySpinner tsScript = floorHit.collider.GetComponent<ToySpinner>();
					Gravity gScript = floorHit.collider.GetComponent<Gravity>();
					if(tsScript){
						tsScript.Push();
					} else if(gScript){
						gScript.EnableGravity();
					} else {
						Rigidbody toyRB = floorHit.collider.attachedRigidbody;
//						Debug.Log("toy touched");
						Vector3 vectDif = floorHit.collider.transform.position - transform.position;
						toyRB.AddForce(vectDif * catPowForce);
						SoundWhenHitHardEnough SWHHEScript = floorHit.collider.GetComponent<SoundWhenHitHardEnough>();
						if(SWHHEScript != null){
							SWHHEScript.OnCollisionEnter(null);
						}
					}


				} else {
					Debug.Log("toy out of range");
				}
			} else {
				if(clickLayer != LayerMask.NameToLayer("Default")){
					Debug.Log("Non default layer clicked " + LayerMask.LayerToName(clickLayer));
				}
				catGoal.transform.position = floorHit.point;
				previousDestZone = null;
				StartCoroutine(WayPointSanityTest());
			}
		}
	}

	IEnumerator WayPointSanityTest(){
		yield return new WaitForSeconds (0.1f);
		Ray wayPoint = new Ray (catGoal.transform.position, -Vector3.up);
		RaycastHit wphinfo;
		if (Physics.Raycast (wayPoint, out wphinfo, 2.0f, ignoreClickTesterLayer)) {
			targetMovePoint = wphinfo.point;
			Vector3 goalAtMyHeight = targetMovePoint;
			goalAtMyHeight.y = transform.position.y;
			targetRotation = Quaternion.LookRotation(goalAtMyHeight - transform.position);
			moving = true;
		} else {
			targetMovePoint = transform.position;
			Debug.Log("Invalid input was rejected because the point was too high from the ground"); 
		}
	}

	void StartJump(Vector3 waypoint){
		jumping = true;
		col.enabled = false;
		targetWayPoint = waypoint;  // how far above surface to jump to
	}

	void EndJump(){
		if(murderRat) {
			Destroy(murderRat.gameObject);
			murderRat = null;
		}
		if(murderFly) {
			Destroy(murderFly.gameObject);
			murderFly = null;
		}
		jumping = false;
		transform.rotation = Quaternion.identity;
		rb.velocity = Vector3.zero;
		col.enabled = true;
	}

	void CatSounds(){
		if(Input.GetKeyDown(KeyCode.M)){
			asource.clip = meows[Random.Range(0, meows.Length)];
			asource.Play();
		}
		if(Input.GetKeyDown(KeyCode.P)){
			asource.clip = purr;
			asource.Play();
		}
	}

	public void MoveToTarget(Transform targetMP = null){
		if (targetMP != null) {
			targetMovePoint = targetMP.transform.position;
		}
		float distToTarget = Vector3.Distance (transform.position, targetMovePoint);
		if (moving == false) {
			return;
		}
		if (jumping) {
			float distToWayPoint = Vector3.Distance (transform.position, targetWayPoint);
			if (distToWayPoint > closeEnoughToTarget){
				transform.LookAt(targetWayPoint);
				if(murderRat) {
					murderRat.enabled = false;
					rb.velocity = transform.forward * leapSpeed * 2.0f;
					if(distToWayPoint < closeEnoughToTarget * 2.0f) {
						rb.velocity = Vector3.zero;
						transform.position = murderRat.transform.position;
					}						
				} else if(murderFly) {
					murderFly.enabled = false;
					rb.velocity = transform.forward * leapSpeed * 2.0f;
					if(distToWayPoint < closeEnoughToTarget * 2.0f) {
						rb.velocity = Vector3.zero;
						transform.position = murderFly.transform.position;
					}						
				} else {
					rb.velocity = transform.forward * leapSpeed;
				}
			} else {
				EndJump();
			}
		} else if (distToTarget > closeEnoughToTarget) {
			Vector3 catPlaneClicked = targetMovePoint;
			catPlaneClicked.y = transform.position.y;
			transform.LookAt (catPlaneClicked);
			
			float savedYSpeed = rb.velocity.y;
			Vector3 velocityWithGravity = transform.forward * catSpeed;
			velocityWithGravity.y = savedYSpeed;
			rb.velocity = velocityWithGravity;

			/*Ray catWhisker = new Ray(transform.position, transform.forward);
			RaycastHit rhinfo;

			if(Physics.Raycast(catWhisker, out rhinfo, whiskerLength, ignoreClickTesterLayer)){
				Debug.Log("Jumping on: " + rhinfo.collider.name);
				Ray destRay = new Ray(rhinfo.collider.transform.position + Vector3.up * jumpHeightTester, -Vector3.up);
				RaycastHit rhwaypoint;
				if(Physics.Raycast(destRay, out rhwaypoint, jumpHeightTester, ignoreClickTesterLayer)){
					StartJump(rhwaypoint.point);
				}
			}*/
			Vector3 goalAtMyHeight = targetMovePoint;
			goalAtMyHeight.y = transform.position.y;
			float lateralDist = Vector3.Distance(transform.position, goalAtMyHeight);
			if(lateralDist < jumpLength){
				float heightDif = Mathf.Abs(transform.position.y - targetMovePoint.y);
				if(heightDif > minJumpHeight && heightDif < maxJumpHeight){
					StartJump(targetMovePoint);
				}
			}

		} else {
			rb.velocity = Vector3.zero;
			moving = false;
			transform.rotation = targetRotation;
		}
	}
	
	// Update is called once per frame
	void Update () {
		if (Input.GetMouseButtonDown (0)) {
			if (betweenLevels == false) {
				MouseTarget ();	
			}
		}
		MoveToTarget ();
		CatSounds ();
		if(anim) {
			if(moving) {
				anim.SetBool("isMoving", true);
			} else {
				anim.SetBool("isMoving", false);
			}
		}
	}
}
