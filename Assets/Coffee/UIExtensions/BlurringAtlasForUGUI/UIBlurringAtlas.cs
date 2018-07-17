using System;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;
using System.IO;

namespace Coffee.UIExtensions
{
	/// <summary>
	/// Blurring effect for uGUI.
	/// </summary>
	[ExecuteInEditMode]
	public class UIBlurringAtlas : UIEffectBase
	{
		//################################
		// Constant or Static Members.
		//################################
		public const string shaderName = "UI/Hidden/UI-Effect-BlurringAtlas";
		const int KernelSize = (7-1)*2;


		//################################
		// Serialize Members.
		//################################
		[SerializeField][Range(0, 1)] float m_Blur = 0.25f;
		[SerializeField] bool m_AdvancedMode;


		//################################
		// Public Members.
		//################################

		/// <summary>
		/// How far is the blurring from the graphic.
		/// </summary>
		public float blur
		{
			get { return m_Blur; }
			set
			{
				value = Mathf.Clamp(value, 0, 1);
				if (!Mathf.Approximately(m_Blur, value))
				{
					m_Blur = Mathf.Clamp(value, 0, 1);
					SetDirty();
				}
			}
		}

		/// <summary>
		/// Modifies the mesh.
		/// </summary>
		public override void ModifyMesh(VertexHelper vh)
		{
			if (!isActiveAndEnabled)
				return;

			vh.GetUIVertexStream(tempVerts);
			vh.Clear();
			var count = tempVerts.Count;


			if (!m_AdvancedMode)
			{
				Vector2 uvMask = new Vector2(Packer.ToFloat(0, 0), Packer.ToFloat(1, 1));

				// Vertex
				for (int i = 0; i < count; i++)
				{
					UIVertex vt = tempVerts[i];
					vt.uv0 = new Vector2(Packer.ToFloat((vt.uv0.x + 0.5f) / 2f, (vt.uv0.y + 0.5f) / 2f), blur);
					vt.uv1 = uvMask;
					tempVerts[i] = vt;
				}

				vh.AddUIVertexTriangleStream(tempVerts);
				tempVerts.Clear();
				return;
			}

			// Bundle
			int bundleSize = targetGraphic is Text ? 6 : count;
			Rect posBounds = default(Rect);
			Rect uvBounds = default(Rect);
			Vector3 size = default(Vector3);
			Vector3 tPos = default(Vector3);
			Vector3 tUV = default(Vector3);

			for (int i = 0; i < count; i += bundleSize)
			{
				// Quadバンドル単位での最大/最小値
				GetBounds(tempVerts, i, bundleSize, ref posBounds, ref uvBounds, true);

				// Pack uv mask.
				Vector2 uvMask = new Vector2(Packer.ToFloat(uvBounds.xMin, uvBounds.yMin), Packer.ToFloat(uvBounds.xMax, uvBounds.yMax));

				// Quad
				for (int j = 0; j < bundleSize; j += 6)
				{
					Vector3 cornerPos1 = tempVerts[i + j + 1].position;
					Vector3 cornerPos2 = tempVerts[i + j + 4].position;

					// 外周Quadかどうか.
					bool hasOuterEdge = (bundleSize == 6)
					                    || !posBounds.Contains(cornerPos1)
					                    || !posBounds.Contains(cornerPos2);
					if (hasOuterEdge)
					{
						Vector3 cornerUv1 = tempVerts[i + j + 1].uv0;
						Vector3 cornerUv2 = tempVerts[i + j + 4].uv0;

						Vector3 centerPos = (cornerPos1 + cornerPos2) / 2;
						Vector3 centerUV = (cornerUv1 + cornerUv2) / 2;
						size = (cornerPos1 - cornerPos2);

						size.x = 1 + (KernelSize - 1) / Mathf.Abs(size.x);
						size.y = 1 + (KernelSize - 1) / Mathf.Abs(size.y);
						size.z = 1 + (KernelSize - 1) / Mathf.Abs(size.z);

						tPos = centerPos - Vector3.Scale(size, centerPos);
						tUV = centerUV - Vector3.Scale(size, centerUV);
					}

					// Vertex
					for (int k = 0; k < 6; k++)
					{
						UIVertex vt = tempVerts[i + j + k];

						Vector3 pos = vt.position;
						Vector2 uv0 = vt.uv0;

						if (hasOuterEdge && (pos.x < posBounds.xMin || posBounds.xMax < pos.x))
						{
							pos.x = pos.x * size.x + tPos.x;
							uv0.x = uv0.x * size.x + tUV.x;
						}
						if (hasOuterEdge && (pos.y < posBounds.yMin || posBounds.yMax < pos.y))
						{
							pos.y = pos.y * size.y + tPos.y;
							uv0.y = uv0.y * size.y + tUV.y;
						}

						vt.uv0 = new Vector2(Packer.ToFloat((uv0.x + 0.5f) / 2f, (uv0.y + 0.5f) / 2f), blur);
						vt.position = pos;
						vt.uv1 = uvMask;

						tempVerts[i + j + k] = vt;
					}
				}
			}

			vh.AddUIVertexTriangleStream(tempVerts);
			tempVerts.Clear();
		}

		#if UNITY_EDITOR
		/// <summary>
		/// Gets the material.
		/// </summary>
		/// <returns>The material.</returns>
		protected override Material GetMaterial()
		{
			return UnityEditor.AssetDatabase.FindAssets("t:Material " + Path.GetFileName(shaderName))
				.Select(x => UnityEditor.AssetDatabase.GUIDToAssetPath(x))
				.Select(x => UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(x))
				.FirstOrDefault();
		}
		#endif

		static void GetBounds(List<UIVertex> verts, int start, int count, ref Rect posBounds, ref Rect uvBounds, bool global)
		{
			Vector2 minPos = new Vector2(float.MaxValue, float.MaxValue);
			Vector2 maxPos = new Vector2(float.MinValue, float.MinValue);
			Vector2 minUV = new Vector2(float.MaxValue, float.MaxValue);
			Vector2 maxUV = new Vector2(float.MinValue, float.MinValue);
			for (int i = start; i < start + count; i++)
			{
				UIVertex vt = verts[i];

				Vector2 uv = vt.uv0;
				Vector3 pos = vt.position;

				// Left-Bottom
				if (minPos.x >= pos.x && minPos.y >= pos.y)
				{
					minPos = pos;
				}
				// Right-Top
				else if (maxPos.x <= pos.x && maxPos.y <= pos.y)
				{
					maxPos = pos;
				}

				// Left-Bottom
				if (minUV.x >= uv.x && minUV.y >= uv.y)
				{
					minUV = uv;
				}
				// Right-Top
				else if (maxUV.x <= uv.x && maxUV.y <= uv.y)
				{
					maxUV = uv;
				}
			}

			// Shrink coordinate for detect edge
			posBounds.Set(minPos.x + 0.001f, minPos.y + 0.001f, maxPos.x - minPos.x - 0.002f, maxPos.y - minPos.y - 0.002f);
			uvBounds.Set(minUV.x, minUV.y, maxUV.x - minUV.x, maxUV.y - minUV.y);
		}
	}
}
