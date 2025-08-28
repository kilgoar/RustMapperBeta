using System;
using System.Collections.Generic;
using UnityEngine;

// Token: 0x02001134 RID: 4404
public class PathInterpolator
{
	// Token: 0x1700079D RID: 1949
	// (get) Token: 0x060063B8 RID: 25528 RVA: 0x00225025 File Offset: 0x00223225
	// (set) Token: 0x060063B9 RID: 25529 RVA: 0x0022502D File Offset: 0x0022322D
	public int MinIndex { get; set; }

	// Token: 0x1700079E RID: 1950
	// (get) Token: 0x060063BA RID: 25530 RVA: 0x00225036 File Offset: 0x00223236
	// (set) Token: 0x060063BB RID: 25531 RVA: 0x0022503E File Offset: 0x0022323E
	public int MaxIndex { get; set; }

	// Token: 0x1700079F RID: 1951
	// (get) Token: 0x060063BC RID: 25532 RVA: 0x00225047 File Offset: 0x00223247
	// (set) Token: 0x060063BD RID: 25533 RVA: 0x0022504F File Offset: 0x0022324F
	public virtual float Length { get; private set; }

	// Token: 0x170007A0 RID: 1952
	// (get) Token: 0x060063BE RID: 25534 RVA: 0x00225058 File Offset: 0x00223258
	// (set) Token: 0x060063BF RID: 25535 RVA: 0x00225060 File Offset: 0x00223260
	public virtual float StepSize { get; private set; }

	// Token: 0x170007A1 RID: 1953
	// (get) Token: 0x060063C0 RID: 25536 RVA: 0x00225069 File Offset: 0x00223269
	// (set) Token: 0x060063C1 RID: 25537 RVA: 0x00225071 File Offset: 0x00223271
	public bool Circular { get; private set; }

	// Token: 0x170007A2 RID: 1954
	// (get) Token: 0x060063C2 RID: 25538 RVA: 0x00008330 File Offset: 0x00006530
	public int DefaultMinIndex
	{
		get
		{
			return 0;
		}
	}

	// Token: 0x170007A3 RID: 1955
	// (get) Token: 0x060063C3 RID: 25539 RVA: 0x0022507A File Offset: 0x0022327A
	public int DefaultMaxIndex
	{
		get
		{
			return this.Points.Length - 1;
		}
	}

	// Token: 0x170007A4 RID: 1956
	// (get) Token: 0x060063C4 RID: 25540 RVA: 0x00225086 File Offset: 0x00223286
	public float StartOffset
	{
		get
		{
			return this.Length * (float)(this.MinIndex - this.DefaultMinIndex) / (float)(this.DefaultMaxIndex - this.DefaultMinIndex);
		}
	}

	// Token: 0x170007A5 RID: 1957
	// (get) Token: 0x060063C5 RID: 25541 RVA: 0x002250AC File Offset: 0x002232AC
	public float EndOffset
	{
		get
		{
			return this.Length * (float)(this.DefaultMaxIndex - this.MaxIndex) / (float)(this.DefaultMaxIndex - this.DefaultMinIndex);
		}
	}

	// Token: 0x060063C6 RID: 25542 RVA: 0x002250D4 File Offset: 0x002232D4
	public PathInterpolator(Vector3[] points)
	{
		if (points.Length < 2)
		{
			throw new ArgumentException("Point list too short.");
		}
		this.Points = points;
		this.MinIndex = this.DefaultMinIndex;
		this.MaxIndex = this.DefaultMaxIndex;
		this.Circular = (Vector3.Distance(points[0], points[points.Length - 1]) < 0.1f);
	}

	// Token: 0x060063C7 RID: 25543 RVA: 0x0022513C File Offset: 0x0022333C
	public PathInterpolator(Vector3[] points, Vector3[] tangents) : this(points)
	{
		if (tangents.Length != points.Length)
		{
			throw new ArgumentException("Points and tangents lengths must match. Points: " + points.Length.ToString() + " Tangents: " + tangents.Length.ToString());
		}
		this.Tangents = tangents;
		this.RecalculateLength();
		this.initialized = true;
	}

	// Token: 0x060063C8 RID: 25544 RVA: 0x00225198 File Offset: 0x00223398
	public void RecalculateTangents()
	{
		if (this.Tangents == null || this.Tangents.Length != this.Points.Length)
		{
			this.Tangents = new Vector3[this.Points.Length];
		}
		for (int i = 0; i < this.Points.Length; i++)
		{
			int num = i - 1;
			int num2 = i + 1;
			if (num < 0)
			{
				num = (this.Circular ? (this.Points.Length - 2) : 0);
			}
			if (num2 > this.Points.Length - 1)
			{
				num2 = (this.Circular ? 1 : (this.Points.Length - 1));
			}
			Vector3 b = this.Points[num];
			Vector3 a = this.Points[num2];
			this.Tangents[i] = (a - b).normalized;
		}
		this.RecalculateLength();
		this.initialized = true;
	}

	// Token: 0x060063C9 RID: 25545 RVA: 0x00225278 File Offset: 0x00223478
	public void RecalculateLength()
	{
		float num = 0f;
		for (int i = 0; i < this.Points.Length - 1; i++)
		{
			Vector3 b = this.Points[i];
			Vector3 a = this.Points[i + 1];
			num += (a - b).magnitude;
		}
		this.Length = num;
		this.StepSize = num / (float)this.Points.Length;
	}

	// Token: 0x060063CA RID: 25546 RVA: 0x002252E8 File Offset: 0x002234E8
	public void Resample(float distance)
	{
		float num = 0f;
		for (int i = 0; i < this.Points.Length - 1; i++)
		{
			Vector3 b = this.Points[i];
			Vector3 a = this.Points[i + 1];
			num += (a - b).magnitude;
		}
		int num2 = Mathf.RoundToInt(num / distance);
		if (num2 < 2)
		{
			return;
		}
		distance = num / (float)(num2 - 1);
		List<Vector3> list = new List<Vector3>(num2);
		float num3 = 0f;
		for (int j = 0; j < this.Points.Length - 1; j++)
		{
			int num4 = j;
			int num5 = j + 1;
			Vector3 vector = this.Points[num4];
			Vector3 vector2 = this.Points[num5];
			float num6 = (vector2 - vector).magnitude;
			if (num4 == 0)
			{
				list.Add(vector);
			}
			while (num3 + num6 > distance)
			{
				float num7 = distance - num3;
				float t = num7 / num6;
				Vector3 vector3 = Vector3.Lerp(vector, vector2, t);
				list.Add(vector3);
				vector = vector3;
				num3 = 0f;
				num6 -= num7;
			}
			num3 += num6;
			if (num5 == this.Points.Length - 1 && num3 > distance * 0.5f)
			{
				list.Add(vector2);
			}
		}
		if (list.Count < 2)
		{
			return;
		}
		this.Points = list.ToArray();
		this.MinIndex = this.DefaultMinIndex;
		this.MaxIndex = this.DefaultMaxIndex;
		this.initialized = false;
	}

	// Token: 0x060063CB RID: 25547 RVA: 0x00225468 File Offset: 0x00223668
	public void Smoothen(int iterations, Func<int, float> filter = null)
	{
		this.Smoothen(iterations, Vector3.one, filter);
	}

	// Token: 0x060063CC RID: 25548 RVA: 0x00225478 File Offset: 0x00223678
	public void Smoothen(int iterations, Vector3 multipliers, Func<int, float> filter = null)
	{
		for (int i = 0; i < iterations; i++)
		{
			for (int j = this.MinIndex + (this.Circular ? 0 : 1); j <= this.MaxIndex - 1; j += 2)
			{
				this.SmoothenIndex(j, multipliers, filter);
			}
			for (int k = this.MinIndex + (this.Circular ? 1 : 2); k <= this.MaxIndex - 1; k += 2)
			{
				this.SmoothenIndex(k, multipliers, filter);
			}
		}
		this.initialized = false;
	}

	// Token: 0x060063CD RID: 25549 RVA: 0x002254F4 File Offset: 0x002236F4
	private void SmoothenIndex(int i, Vector3 multipliers, Func<int, float> filter = null)
	{
		int num = i - 1;
		int num2 = i + 1;
		if (i == 0)
		{
			num = this.Points.Length - 2;
		}
		Vector3 a = this.Points[num];
		Vector3 vector = this.Points[i];
		Vector3 b = this.Points[num2];
		Vector3 vector2 = (a + vector + vector + b) * 0.25f;
		if (filter != null)
		{
			multipliers *= filter(i);
		}
		if (multipliers != Vector3.one)
		{
			vector2.x = Mathf.LerpUnclamped(vector.x, vector2.x, multipliers.x);
			vector2.y = Mathf.LerpUnclamped(vector.y, vector2.y, multipliers.y);
			vector2.z = Mathf.LerpUnclamped(vector.z, vector2.z, multipliers.z);
		}
		this.Points[i] = vector2;
		if (i == 0)
		{
			this.Points[this.Points.Length - 1] = this.Points[0];
		}
	}

	// Token: 0x060063CE RID: 25550 RVA: 0x00225610 File Offset: 0x00223810
	public void Straighten(int diStart, int diEnd)
	{
		Vector3 a = this.Points[diStart];
		Vector3 b = this.Points[diEnd];
		Vector3 a2 = this.Tangents[diStart];
		Vector3 b2 = this.Tangents[diEnd];
		float num = 1f / (float)(diEnd - diStart);
		for (int i = diStart + 1; i <= diEnd - 1; i++)
		{
			float t = (float)(i - diStart) * num;
			this.Points[i] = Vector3.Lerp(a, b, t);
			this.Tangents[i] = Vector3.Slerp(a2, b2, t);
		}
	}

	// Token: 0x060063CF RID: 25551 RVA: 0x002256A7 File Offset: 0x002238A7
	public Vector3 GetStartPoint()
	{
		return this.Points[this.MinIndex];
	}

	// Token: 0x060063D0 RID: 25552 RVA: 0x002256BA File Offset: 0x002238BA
	public Vector3 GetEndPoint()
	{
		return this.Points[this.MaxIndex];
	}

	// Token: 0x060063D1 RID: 25553 RVA: 0x002256CD File Offset: 0x002238CD
	public Vector3 GetStartTangent()
	{
		if (!this.initialized)
		{
			throw new Exception("Tangents have not been calculated yet or are outdated.");
		}
		return this.Tangents[this.MinIndex];
	}

	// Token: 0x060063D2 RID: 25554 RVA: 0x002256F3 File Offset: 0x002238F3
	public Vector3 GetEndTangent()
	{
		if (!this.initialized)
		{
			throw new Exception("Tangents have not been calculated yet or are outdated.");
		}
		return this.Tangents[this.MaxIndex];
	}

	// Token: 0x060063D3 RID: 25555 RVA: 0x0022571C File Offset: 0x0022391C
	public Vector3 GetPointByIndex(int i)
	{
		if (!this.Circular)
		{
			return this.Points[Mathf.Clamp(i, 0, this.Points.Length - 1)];
		}
		return this.Points[(i % this.Points.Length + this.Points.Length) % this.Points.Length];
	}

	// Token: 0x060063D4 RID: 25556 RVA: 0x00225778 File Offset: 0x00223978
	public Vector3 GetTangentByIndex(int i)
	{
		return (this.GetPoint(i + 1) - this.GetPoint(i - 1)).normalized;
	}

	// Token: 0x060063D5 RID: 25557 RVA: 0x002257A4 File Offset: 0x002239A4
	public int GetPrevIndex(float distance)
	{
		return Mathf.FloorToInt(distance / this.Length * (float)(this.Points.Length - 1));
	}

	// Token: 0x060063D6 RID: 25558 RVA: 0x002257BF File Offset: 0x002239BF
	public int GetNextIndex(float distance)
	{
		return Mathf.CeilToInt(distance / this.Length * (float)(this.Points.Length - 1));
	}

	// Token: 0x060063D7 RID: 25559 RVA: 0x002257DC File Offset: 0x002239DC
	public Vector3 GetPoint(int index)
	{
		if (this.Length == 0f)
		{
			return this.GetStartPoint();
		}
		if (index <= this.MinIndex)
		{
			return this.GetStartPoint();
		}
		if (index >= this.MaxIndex)
		{
			return this.GetEndPoint();
		}
		return this.Points[index];
	}

	// Token: 0x060063D8 RID: 25560 RVA: 0x0022582C File Offset: 0x00223A2C
	public Vector3 GetPoint(float distance)
	{
		if (this.Length == 0f)
		{
			return this.GetStartPoint();
		}
		float num = distance / this.Length * (float)(this.Points.Length - 1);
		int num2 = (int)num;
		if (num <= (float)this.MinIndex)
		{
			return this.GetStartPoint();
		}
		if (num >= (float)this.MaxIndex)
		{
			return this.GetEndPoint();
		}
		Vector3 a = this.Points[num2];
		Vector3 b = this.Points[num2 + 1];
		float t = num - (float)num2;
		return Vector3.Lerp(a, b, t);
	}

	// Token: 0x060063D9 RID: 25561 RVA: 0x002258B0 File Offset: 0x00223AB0
	public virtual Vector3 GetTangent(float distance)
	{
		if (!this.initialized)
		{
			throw new Exception("Tangents have not been calculated yet or are outdated.");
		}
		if (this.Length == 0f)
		{
			return this.GetStartPoint();
		}
		float num = distance / this.Length * (float)(this.Tangents.Length - 1);
		int num2 = (int)num;
		if (num <= (float)this.MinIndex)
		{
			return this.GetStartTangent();
		}
		if (num >= (float)this.MaxIndex)
		{
			return this.GetEndTangent();
		}
		Vector3 a = this.Tangents[num2];
		Vector3 b = this.Tangents[num2 + 1];
		float t = num - (float)num2;
		return Vector3.Slerp(a, b, t);
	}

	// Token: 0x060063DA RID: 25562 RVA: 0x00225948 File Offset: 0x00223B48
	public virtual Vector3 GetPointCubicHermite(float distance)
	{
		if (!this.initialized)
		{
			throw new Exception("Tangents have not been calculated yet or are outdated.");
		}
		if (this.Length == 0f)
		{
			return this.GetStartPoint();
		}
		float num = distance / this.Length * (float)(this.Points.Length - 1);
		int num2 = (int)num;
		if (num <= (float)this.MinIndex)
		{
			return this.GetStartPoint();
		}
		if (num >= (float)this.MaxIndex)
		{
			return this.GetEndPoint();
		}
		Vector3 a = this.Points[num2];
		Vector3 a2 = this.Points[num2 + 1];
		Vector3 a3 = this.Tangents[num2] * this.StepSize;
		Vector3 a4 = this.Tangents[num2 + 1] * this.StepSize;
		float num3 = num - (float)num2;
		float num4 = num3 * num3;
		float num5 = num3 * num4;
		return (2f * num5 - 3f * num4 + 1f) * a + (num5 - 2f * num4 + num3) * a3 + (-2f * num5 + 3f * num4) * a2 + (num5 - num4) * a4;
	}

	// Token: 0x04005FD3 RID: 24531
	public Vector3[] Points;

	// Token: 0x04005FD4 RID: 24532
	public Vector3[] Tangents;

	// Token: 0x04005FDA RID: 24538
	protected bool initialized;
}
