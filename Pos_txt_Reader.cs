using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System;
using CylinderMove;

// pos.txtのデータ
// https://github.com/miu200521358/3d-pose-baseline-vmd/blob/master/doc/Output.md
// 0 :Hip
// 1 :RHip
// 2 :RKnee
// 3 :RFoot
// 4 :LHip
// 5 :LKnee
// 6 :LFoot
// 7 :Spine
// 8 :Thorax
// 9 :Neck/Nose
// 10:Head
// 11:LShoulder
// 12:LElbow
// 13:LWrist
// 14:RShoulder
// 15:RElbow
// 16:RWrist

public class Pos_txt_Reader : MonoBehaviour
{
    float scale_ratio = 0.001f;  // pos.txtとUnityモデルのスケール比率
	                             // pos.txtの単位はmmでUnityはmのため、0.001に近い値を指定。モデルの大きさによって調整する
	float heal_position = 0.05f; // 足の沈みの補正値(単位：m)。プラス値で体全体が上へ移動する
	float head_angle = 15f; // 顔の向きの調整 顔を15度上げる

	public String pos_filename; // pos.txtのファイル名
	public Boolean debug_cube; // デバッグ用Cubeの表示フラグ
	public int start_frame; // 開始フレーム
	public String end_frame; // 終了フレーム	
	float play_time; // 再生時間 
	Transform[] bone_t; // モデルのボーンのTransform
	Transform[] cube_t; // デバック表示用のCubeのTransform
    Transform[] cylinder_t; // デバック表示用のCubeのTransform
    Vector3 init_position; // 初期のセンターの位置
	Quaternion[] init_rot; // 初期の回転値
	Quaternion[] init_inv; // 初期のボーンの方向から計算されるクオータニオンのInverse
	List<Vector3[]> pos; // pos.txtのデータを保持するコンテナ
	int[] bones = new int[10] { 1, 2, 4, 5, 7, 8, 11, 12, 14, 15 }; // 親ボーン
 	int[] child_bones = new int[10] { 2, 3, 5, 6, 8, 10, 12, 13, 15, 16 }; // bonesに対応する子ボーン
 	int bone_num = 17;
	Animator anim;
	int s_frame;
	int e_frame;


    private Transform cylinderPrefab;

    private GameObject leftSphere;
    private GameObject rightSphere;
    private GameObject cylinder;

    // pos.txtのデータを読み込み、リストで返す
    List<Vector3[]> ReadPosData(string filename) {
		List<Vector3[]> data = new List<Vector3[]>();

        //一行ずつ読んでlinesに入れる
		List<string> lines = new List<string>();
		StreamReader sr = new StreamReader(filename);
		while (!sr.EndOfStream) {
			lines.Add(sr.ReadLine());
		}
		sr.Close();


		foreach (string line in lines) {
			string line2 = line.Replace(",", "");
			string[] str = line2.Split(new string[] { " " }, System.StringSplitOptions.RemoveEmptyEntries); // スペースで分割し、空の文字列は削除

			Vector3[] vs = new Vector3[bone_num];
            //pos.txtのボーンの番号だけ回すから4飛ばし
			for (int i = 0; i < str.Length; i += 4) {
				vs[(int)(i/4)] = new Vector3(-float.Parse(str[i + 1]), float.Parse(str[i + 3]), -float.Parse(str[i + 2]));//なぜここでyとzを逆にしているのか？？x→-x,y→z,z→-y
			}
			data.Add(vs);
		}
		return data;
	}

	// BoneTransformの取得。回転の初期値を取得
	void GetInitInfo()
	{
		bone_t = new Transform[bone_num];
		init_rot = new Quaternion[bone_num];
		init_inv = new Quaternion[bone_num];

		bone_t[0] = anim.GetBoneTransform(HumanBodyBones.Hips);
		bone_t[1] = anim.GetBoneTransform(HumanBodyBones.RightUpperLeg);
		bone_t[2] = anim.GetBoneTransform(HumanBodyBones.RightLowerLeg);
		bone_t[3] = anim.GetBoneTransform(HumanBodyBones.RightFoot);
		bone_t[4] = anim.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
		bone_t[5] = anim.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
		bone_t[6] = anim.GetBoneTransform(HumanBodyBones.LeftFoot);
		bone_t[7] = anim.GetBoneTransform(HumanBodyBones.Spine);
		bone_t[8] = anim.GetBoneTransform(HumanBodyBones.Neck);
		bone_t[10] = anim.GetBoneTransform(HumanBodyBones.Head);
		bone_t[11] = anim.GetBoneTransform(HumanBodyBones.LeftUpperArm);
		bone_t[12] = anim.GetBoneTransform(HumanBodyBones.LeftLowerArm);
		bone_t[13] = anim.GetBoneTransform(HumanBodyBones.LeftHand);
		bone_t[14] = anim.GetBoneTransform(HumanBodyBones.RightUpperArm);
		bone_t[15] = anim.GetBoneTransform(HumanBodyBones.RightLowerArm);
		bone_t[16] = anim.GetBoneTransform(HumanBodyBones.RightHand);

		// Spine,LHip,RHipで三角形を作ってそれを前方向とする。
		Vector3 init_forward = TriangleNormal(bone_t[7].position, bone_t[4].position, bone_t[1].position);
		init_inv[0] = Quaternion.Inverse(Quaternion.LookRotation(init_forward));

		init_position = bone_t[0].position;
		init_rot[0] = bone_t[0].rotation;
		for (int i = 0; i < bones.Length; i++) {
			int b = bones[i];
			int cb = child_bones[i];

			// 対象モデルの回転の初期値
			init_rot[b] = bone_t[b].rotation;
			// 初期のボーンの方向から計算されるクオータニオン
			init_inv[b] = Quaternion.Inverse(Quaternion.LookRotation(bone_t[b].position - bone_t[cb].position,init_forward));
		}
	}

	// 指定の3点でできる三角形に直交する長さ1のベクトルを返す
	Vector3 TriangleNormal(Vector3 a, Vector3 b, Vector3 c)
	{
		Vector3 d1 = a - b;
		Vector3 d2 = a - c;

		Vector3 dd = Vector3.Cross(d1, d2);
		dd.Normalize();

		return dd;
	}

	// デバック用cubeを生成する。生成済みの場合は位置を更新する
	void UpdateCube(int frame)
	{
		if (cube_t == null) {
			// 初期化して、cubeを生成する
			cube_t = new Transform[bone_num];
            //cylinder_t = new Transform[bone_num];

			for (int i = 0; i < bone_num; i++) {
				Transform t = GameObject.CreatePrimitive(PrimitiveType.Cube).transform;
				//t.transform.parent = this.transform;
				t.localPosition = pos[frame][i] * scale_ratio;
				t.name = i.ToString();
				t.localScale = new Vector3(0.3f, 0.3f, 0.3f);
				cube_t[i] = t;

				Destroy(t.GetComponent<BoxCollider>());

                /*
                //棒部分
                Transform t_b = GameObject.CreatePrimitive(PrimitiveType.Cylinder).transform;
                t_b.transform.parent = this.transform;
                t_b.localPosition = pos[frame][i] * scale_ratio;
                t_b.localScale = new Vector3(0.01f, 1f, 0.01f);
                t_b.GetComponent<Renderer>().material.color = Color.red;
                cylinder_t[i] = t_b;
                */
            }
		}
		else {



            // モデルと重ならないように少しずらして表示
            Vector3 offset = new Vector3(1.2f, 0f, 0f);


            // ConnectBox.Instance.Connect(pos[frame][10] * scale_ratio + new Vector3(0, heal_position, 0) + offset, pos[frame][9] * scale_ratio + new Vector3(0, heal_position, 0) + offset);


            //Smoothingかける＋offsetのプラスなどを簡易表現
            Vector3[] easypos = new Vector3[17];
            for (int i = 0; i < bone_num; i++)
            {
                //easypos[i] = Smooth(pos, frame, i) * 0.01f + new Vector3(0, heal_position, 0) + offset;
                easypos[i] = Smooth(pos, frame, i)*0.01f;

                cube_t[i].localPosition = easypos[i];
                //easypos[i] = pos[frame][i] * scale_ratio + new Vector3(0, heal_position, 0) + offset;
            }
            Debug.Log(easypos[16]);



            /*
            // 初期化済みの場合は、cubeの位置を更新する
            for (int i = 0; i < bone_num; i++) {
                //Vector3 smoothedpos= Smooth(easypos,i); 
                cube_t[i].localPosition= smoothedpos;

                //		cube_t[i].localPosition = pos[frame][i] * scale_ratio + new Vector3(0, heal_position, 0) + offset;
                // ConnectBox connectBox=null;
                //connectBox.Connect(pos[frame][0],pos[frame][10]);
                // cylinder_t[i].localPosition = pos[frame][i] * scale_ratio + new Vector3(0, heal_position, 0) + offset; ;
            }
            */



            ConnectBox.Instance.Connect(easypos[10], easypos[9]);
            ConnectBox.Instance.Connect(easypos[9], easypos[8]);
            ConnectBox.Instance.Connect(easypos[8], easypos[14]);
            ConnectBox.Instance.Connect(easypos[14], easypos[15]);
            ConnectBox.Instance.Connect(easypos[15], easypos[16]);
            ConnectBox.Instance.Connect(easypos[8], easypos[11]);
            ConnectBox.Instance.Connect(easypos[11], easypos[12]);
            ConnectBox.Instance.Connect(easypos[12], easypos[13]);
            ConnectBox.Instance.Connect(easypos[8], easypos[7]);
            ConnectBox.Instance.Connect(easypos[7], easypos[0]);
            ConnectBox.Instance.Connect(easypos[0], easypos[1]);
            ConnectBox.Instance.Connect(easypos[1], easypos[2]);
            ConnectBox.Instance.Connect(easypos[2], easypos[3]);
            ConnectBox.Instance.Connect(easypos[0], easypos[4]);
            ConnectBox.Instance.Connect(easypos[4], easypos[5]);
            ConnectBox.Instance.Connect(easypos[5], easypos[6]);
            /*
            ConnectBox.Instance.Connect(easypos[x],easypos[y]);
            ConnectBox.Instance.Connect(pos[frame][10] * scale_ratio + new Vector3(0, heal_position, 0) + offset, pos[frame][9] * scale_ratio + new Vector3(0, heal_position, 0) + offset);
            ConnectBox.Instance.Connect(pos[frame][9] * scale_ratio + new Vector3(0, heal_position, 0) + offset, pos[frame][8] * scale_ratio + new Vector3(0, heal_position, 0) + offset);
            ConnectBox.Instance.Connect(pos[frame][8] * scale_ratio + new Vector3(0, heal_position, 0) + offset, pos[frame][14] * scale_ratio + new Vector3(0, heal_position, 0) + offset);
            ConnectBox.Instance.Connect(pos[frame][14] * scale_ratio + new Vector3(0, heal_position, 0) + offset, pos[frame][15] * scale_ratio + new Vector3(0, heal_position, 0) + offset);
            ConnectBox.Instance.Connect(pos[frame][15] * scale_ratio + new Vector3(0, heal_position, 0) + offset, pos[frame][16] * scale_ratio + new Vector3(0, heal_position, 0) + offset);
            ConnectBox.Instance.Connect(pos[frame][8] * scale_ratio + new Vector3(0, heal_position, 0) + offset, pos[frame][11] * scale_ratio + new Vector3(0, heal_position, 0) + offset);
            ConnectBox.Instance.Connect(pos[frame][11] * scale_ratio + new Vector3(0, heal_position, 0) + offset, pos[frame][12] * scale_ratio + new Vector3(0, heal_position, 0) + offset);
            ConnectBox.Instance.Connect(pos[frame][12] * scale_ratio + new Vector3(0, heal_position, 0) + offset, pos[frame][13] * scale_ratio + new Vector3(0, heal_position, 0) + offset);
            ConnectBox.Instance.Connect(pos[frame][8] * scale_ratio + new Vector3(0, heal_position, 0) + offset, pos[frame][7] * scale_ratio + new Vector3(0, heal_position, 0) + offset);
            ConnectBox.Instance.Connect(pos[frame][7] * scale_ratio + new Vector3(0, heal_position, 0) + offset, pos[frame][0] * scale_ratio + new Vector3(0, heal_position, 0) + offset);
            ConnectBox.Instance.Connect(pos[frame][0] * scale_ratio + new Vector3(0, heal_position, 0) + offset, pos[frame][1] * scale_ratio + new Vector3(0, heal_position, 0) + offset);
            ConnectBox.Instance.Connect(pos[frame][1] * scale_ratio + new Vector3(0, heal_position, 0) + offset, pos[frame][2] * scale_ratio + new Vector3(0, heal_position, 0) + offset);
            ConnectBox.Instance.Connect(pos[frame][2] * scale_ratio + new Vector3(0, heal_position, 0) + offset, pos[frame][3] * scale_ratio + new Vector3(0, heal_position, 0) + offset);
            ConnectBox.Instance.Connect(pos[frame][0] * scale_ratio + new Vector3(0, heal_position, 0) + offset, pos[frame][4] * scale_ratio + new Vector3(0, heal_position, 0) + offset);
            ConnectBox.Instance.Connect(pos[frame][4] * scale_ratio + new Vector3(0, heal_position, 0) + offset, pos[frame][5] * scale_ratio + new Vector3(0, heal_position, 0) + offset);
            ConnectBox.Instance.Connect(pos[frame][5] * scale_ratio + new Vector3(0, heal_position, 0) + offset, pos[frame][6] * scale_ratio + new Vector3(0, heal_position, 0) + offset);

            */

        }
    }
    //Smooth(easypos[x]),Smooth(easypos[x-1]);
    //smoothing
    
    Vector3 Smooth(List<Vector3[]> _pos,int frame, int boneNum) {
        Vector3[] prepreF = new Vector3[17];
        Vector3[] preF = new Vector3[17];
        Vector3[] nextF = new Vector3[boneNum];
        Vector3[] nexnextF = new Vector3[boneNum];
        if (frame>2)
        {
            prepreF = _pos[frame - 2];
        }
        else
        {
            prepreF = _pos[frame];
        }

        if (frame>1)
        {
             preF = _pos[frame - 1];
        }
        else
        {
             preF = _pos[frame];
        }

        if (pos[frame + 1] != null)
        {
            nextF = _pos[frame + 1];
        }
        else
        {
            nextF = _pos[frame];
        }

        if (pos[frame +2] != null)
        {
            nexnextF = _pos[frame +2];
        }
        else
        {
            nexnextF = _pos[frame];
        }

        Vector3[] nowF = new Vector3[17];
        nowF = _pos[frame];
       

        //Vector3[] smoothpos = new Vector3[17];
        for(int i = 0; i < 1000; i++)
        {
            nowF[boneNum] = (prepreF[boneNum] + 2f * preF[boneNum] + 4f * nowF[boneNum] + 2f * nextF[boneNum] + nexnextF[boneNum]) / 10f;
            
        }
        
        //smoothpos[frame] = ( nowF[boneNum]);

        return nowF[boneNum];
    }
    
    /*
    Vector3 Smooth(List<Vector3[]> _pos, int frame, int boneNum)
    {
        Vector3[] preF = new Vector3[17];
        Vector3[] nextF = new Vector3[boneNum];

        if (pos[frame - 1] != null)
        {
            preF = _pos[frame - 1];
        }
        else
        {
            preF = _pos[frame];
        }

        if (pos[frame + 1] != null)
        {
            nextF = _pos[frame + 1];
        }
        else
        {
            nextF = _pos[frame];
        }

   


        Vector3[] nowF = _pos[frame];


        // Vector3[] smoothpos = new Vector3[frame + 20];
        //Vector3[] smoothpos = new Vector3[17];
        for(int i = 0; i < 10000; i++)
        {
            nowF[boneNum] = ((preF[boneNum] +  nowF[boneNum] + nextF[boneNum]) / 3);
        }
        
        //smoothpos[boneNum] = ((preF[boneNum] + 2 * smoothpos[boneNum] + nextF[boneNum]) / 4);
        //smoothpos[frame] = ((preF[boneNum] + 2 * smoothpos[boneNum] + nextF[boneNum]) / 4);
        //smoothpos[frame] = ( nowF[boneNum]);

        return nowF[boneNum];
    }
    */

    void Start()
	{
        /*
        leftSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        rightSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        leftSphere.transform.position = new Vector3(-1, 0, 0);
        rightSphere.transform.position = new Vector3(1, 0, 0);
        InstantiateCylinder(cylinderPrefab, leftSphere.transform.position, rightSphere.transform.position);
        */

        anim = GetComponent<Animator>();
		play_time = 0;
		if (System.IO.File.Exists (pos_filename) == false) {
			Debug.Log("<color=blue>Error! Pos file not found(" + pos_filename + "). Check Pos_filename in Inspector.</color>");
		}
		pos = ReadPosData(pos_filename);
		GetInitInfo();
		if (pos != null) {
			// inspectorで指定した開始フレーム、終了フレーム番号をセット
			if (start_frame >= 0 && start_frame < pos.Count) {
				s_frame = start_frame;
			} else {
				s_frame = 0;
			}
			int ef;
			if (int.TryParse(end_frame, out ef)) {
				if (ef >= s_frame && ef < pos.Count) {
					e_frame = ef;
				} else {
					e_frame = pos.Count - 1;
				}
			} else {
				e_frame = pos.Count - 1;
			}
			Debug.Log("End Frame:" + e_frame.ToString());
		}
	}

	void Update()
	{
        ConnectBox.Instance.FlagInit();
        /*
        leftSphere.transform.position = new Vector3(-1, -2f * Mathf.Sin(Time.time), 0);
        rightSphere.transform.position = new Vector3(1, 2f * Mathf.Sin(Time.time), 0);
        UpdateCylinderPosition(cylinder, leftSphere.transform.position, rightSphere.transform.position);
        */

        if (pos == null) {
			return;
		}
		play_time += Time.deltaTime;

		int frame = s_frame + (int)(play_time * 30.0f);  // pos.txtは30fpsを想定
		if (frame > e_frame) {
			play_time = 0;  // 繰り返す
			frame = s_frame;
		}

		if (debug_cube) {
			UpdateCube(frame); // デバッグ用Cubeを表示する
            
		}

		Vector3[] now_pos = pos[frame];
	
		// センターの移動と回転
		Vector3 pos_forward = TriangleNormal(now_pos[7], now_pos[4], now_pos[1]);
		bone_t[0].position = now_pos[0] * scale_ratio + new Vector3(init_position.x, heal_position, init_position.z);
		bone_t[0].rotation = Quaternion.LookRotation(pos_forward) * init_inv[0] * init_rot[0];

		// 各ボーンの回転
		for (int i = 0; i < bones.Length; i++) {
			int b = bones[i];
			int cb = child_bones[i];
			bone_t[b].rotation = Quaternion.LookRotation(now_pos[b] - now_pos[cb], pos_forward) * init_inv[b] * init_rot[b];
		}

		// 顔の向きを上げる調整。両肩を結ぶ線を軸として回転
		bone_t[8].rotation = Quaternion.AngleAxis(head_angle, bone_t[11].position - bone_t[14].position) * bone_t[8].rotation;
	}

    public void InstantiateCylinder(Transform cylinderPrefab, Vector3 beginPoint, Vector3 endPoint)
    {
        cylinder = Instantiate<GameObject>(cylinderPrefab.gameObject, Vector3.zero, Quaternion.identity);
        UpdateCylinderPosition(cylinder, beginPoint, endPoint);
    }

    private void UpdateCylinderPosition(GameObject cylinder, Vector3 beginPoint, Vector3 endPoint)
    {
        Vector3 offset = endPoint - beginPoint;
        Vector3 position = beginPoint + (offset / 2.0f);

        cylinder.transform.position = position;
        cylinder.transform.LookAt(beginPoint);
        Vector3 localScale = cylinder.transform.localScale;
        localScale.z = (endPoint - beginPoint).magnitude;
        cylinder.transform.localScale = localScale;
    }
}