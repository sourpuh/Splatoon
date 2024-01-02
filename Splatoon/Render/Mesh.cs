namespace Splatoon.Render;

using Triangle = (int, int, int);

public interface IMesh
{
    public int NumVertices();
    public int NumTriangles();
    public Vector3 Vertex(int index);
    public Triangle Triangle(int index);
}

public class Debug : IMesh
{
    int IMesh.NumTriangles()
    {
        return 1;
    }

    int IMesh.NumVertices()
    {
        return 3;
    }

    (int, int, int) IMesh.Triangle(int index)
    {
        return (0, 1, 2);
    }

    Vector3 IMesh.Vertex(int index)
    {
        switch (index)
        {
            case 0:
                return new(1, 0, 0);
            case 1:
                return new(0, 0, 1);
            case 2:
                return new(0, 0, 0);
            default:
                return new(0, 0, 0);
        }
    }
}
public class Circle : IMesh
{
    Vector3[] vertices;
    Triangle[] triangles;

    public Circle(int segments, float minAngle, float maxAngle)
    {
        int vertexCount = segments + 2;
        vertices = new Vector3[vertexCount];
        triangles = new Triangle[segments];

        float totalAngle = maxAngle - minAngle;
        float angleStep = totalAngle / segments;

        vertices[0] = new(0, 0, 0);
        vertices[1] = new(1, 0, 0);
        for (int step = 0; step < segments; step++)
        {
            float angle = minAngle + (step + 1) * angleStep;
            var x = MathF.Cos(angle);
            var y = MathF.Sin(angle);
            vertices[step + 2] = new(x, 0, y);
            triangles[step] = (0, step + 1, step + 2);
        }
    }

    int IMesh.NumVertices()
    {
        return vertices.Length;
    }

    int IMesh.NumTriangles()
    {
        return triangles.Length;
    }

    Vector3 IMesh.Vertex(int index)
    {
        return vertices[index];
    }

    Triangle IMesh.Triangle(int index)
    {
        return triangles[index];
    }
}

public class Donut : IMesh
{
    Vector3[] vertices;
    Triangle[] triangles;

    public Donut(int segments, float innerRadius, float outerRadius, float minAngle, float maxAngle)
    {
        int vertexCount = 2 * (segments + 1);
        vertices = new Vector3[vertexCount];
        triangles = new Triangle[segments * 2];

        float totalAngle = maxAngle - minAngle;
        float angleStep = totalAngle / segments;

        for (int step = 0; step <= segments; step++)
        {
            float angle = minAngle + step * angleStep;
            var x = MathF.Cos(angle);
            var y = MathF.Sin(angle);
            vertices[2 * step] = innerRadius * new Vector3(x, 0, y);
            vertices[2 * step + 1] = outerRadius * new Vector3(x, 0, y);

            int triangle = 2 * (step - 1);

            if (triangle >= 0)
            {
                int vertex1 = 2 * (step - 1);
                int vertex2 = 2 * (step - 1) + 1;
                int vertex3 = 2 * (step);
                int vertex4 = 2 * (step) + 1;
                triangles[triangle] = (vertex1, vertex2, vertex3);
                triangles[triangle + 1] = (vertex3, vertex2, vertex4);
            }
        }
    }

    int IMesh.NumVertices()
    {
        return vertices.Length;
    }

    int IMesh.NumTriangles()
    {
        return triangles.Length;
    }

    Vector3 IMesh.Vertex(int index)
    {
        return vertices[index];
    }

    Triangle IMesh.Triangle(int index)
    {
        return triangles[index];
    }
}

public class Line : IMesh
{
    Vector3[] vertices;
    Triangle[] triangles;

    public Line(Vector3 direction, float radius)
    {
        vertices = new Vector3[4];
        triangles = new Triangle[2];

        var perpendicularRadius = radius * Vector3.Normalize(Vector3.Cross(direction, Vector3.UnitY));
        vertices[0] = perpendicularRadius;
        vertices[1] = direction + perpendicularRadius;
        vertices[2] = -perpendicularRadius;
        vertices[3] = direction - perpendicularRadius;

        triangles[0] = (0, 2, 1);
        triangles[1] = (1, 2, 3);
    }

    int IMesh.NumVertices()
    {
        return vertices.Length;
    }

    int IMesh.NumTriangles()
    {
        return triangles.Length;
    }

    Vector3 IMesh.Vertex(int index)
    {
        return vertices[index];
    }

    Triangle IMesh.Triangle(int index)
    {
        return triangles[index];
    }
}