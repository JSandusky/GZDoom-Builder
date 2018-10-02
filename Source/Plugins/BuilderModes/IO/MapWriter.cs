using System;
using System.Collections.Generic;
using System.Text;
using CodeImp.DoomBuilder.Map;
using System.Xml;
using CodeImp.DoomBuilder.Geometry;
using System.Linq;

namespace GdxExport
{


    public class MapWriter
    {

        void writeVertices(MapSet map, XmlWriter writer)
        {
            writer.WriteStartElement("vertices");
            foreach (Vertex vert in map.Vertices)
            {
                writer.WriteStartElement("v");
                writer.WriteAttributeString("idx", vert.Index.ToString());
                writer.WriteAttributeString("x", vert.Position.x.ToString());
                writer.WriteAttributeString("y", vert.Position.y.ToString());
                writeFields(vert.Fields, writer);
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
        }

        void writeSectors(MapSet map, XmlWriter writer)
        {
            writer.WriteStartElement("sectors");
            foreach (Sector s in map.Sectors)
            {
                writer.WriteStartElement("sector");
                writer.WriteAttributeString("idx", s.Index.ToString());
                writer.WriteAttributeString("tag", s.Tag.ToString());
                writer.WriteAttributeString("ceiling-height", s.CeilHeight.ToString());
                writer.WriteAttributeString("floor-height", s.FloorHeight.ToString());
                writer.WriteAttributeString("ceiling-tex", s.CeilTexture);
                writer.WriteAttributeString("floor-tex", s.FloorTexture);
                writer.WriteAttributeString("lighting", s.Brightness.ToString());
                writer.WriteAttributeString("special", s.Effect.ToString());
                
                StringBuilder sb = new StringBuilder();
                bool anyWritten = false;
                foreach (Sidedef side in s.Sidedefs)
                {
                    if (anyWritten)
                        sb.Append(",");
                    sb.Append(side.Index.ToString());
                    anyWritten = true;
                }
                writer.WriteElementString("sides", sb.ToString());

                //sb = new StringBuilder();
                //anyWritten = false;
                //foreach (var vert in s.FlatVertices)
                //{
                //    int idx = map.Vertices.FirstOrDefault(v => v.Position == new Vector2D(vert.x, vert.y)).Index;
                //    if (anyWritten)
                //        sb.Append(",");
                //    sb.Append(idx.ToString());
                //}
                //if (anyWritten)
                //    writer.WriteElementString("tris", sb.ToString());

                writeFields(s.Fields, writer);

                writer.WriteEndElement();
            }
            writer.WriteEndElement();
        }

        void writeLines(MapSet map, XmlWriter writer)
        {
            writer.WriteStartElement("lines");

            foreach (Linedef l in map.Linedefs)
            {
                writer.WriteStartElement("line");

                writer.WriteAttributeString("idx", l.Index.ToString());
                writer.WriteAttributeString("action", l.Action.ToString());
                writer.WriteAttributeString("tag", l.Tag.ToString());
                writer.WriteAttributeString("activate", l.Activate.ToString());
                if (l.Args.Length > 0)
                {
                    StringBuilder sb = new StringBuilder();
                    bool any = false;
                    foreach (int i in l.Args)
                    {
                        if (any)
                            sb.Append(",");
                        sb.Append(i.ToString());
                        any = true;
                    }
                    writer.WriteAttributeString("action-args", sb.ToString());
                }

                var vert1 = map.Vertices.FirstOrDefault(v => v.Position.x == l.Line.v1.x && v.Position.y == l.Line.v1.y);
                var vert2 = map.Vertices.FirstOrDefault(v => v.Position.x == l.Line.v2.x && v.Position.y == l.Line.v2.y);
                writer.WriteAttributeString("vert-start", vert1.Index.ToString());
                writer.WriteAttributeString("vert-end", vert2.Index.ToString());
                //MapWriter.writeVertex(writer, l.Line.v1);
                //MapWriter.writeVertex(writer, l.Line.v2);

                writer.WriteStartElement("flags");
                foreach (String str in l.GetFlags().Keys)
                {
                    bool value = l.GetFlags()[str];
                    if (value == false)
                        continue;
                    writer.WriteStartElement("flag");
                    writer.WriteAttributeString("name", str);
                    writer.WriteAttributeString("value", value.ToString());
                    writer.WriteEndElement();
                }
                writer.WriteEndElement();

                writeFields(l.Fields, writer);

                writer.WriteEndElement();
            }
            writer.WriteEndElement();

        }

        void writeFields(UniFields fields, XmlWriter writer)
        {
            writer.WriteStartElement("fields");
            foreach (String str in fields.Keys)
            {
                writer.WriteStartElement("field");
                writer.WriteAttributeString("name", str);
                writer.WriteAttributeString("type", fields[str].Type.ToString());
                writer.WriteCData(fields[str].Value.ToString());
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
        }

        void writeSides(MapSet map, XmlWriter writer)
        {
            writer.WriteStartElement("sides");
            foreach (Sidedef s in map.Sidedefs)
            {
                writer.WriteStartElement("side");
                writer.WriteAttributeString("idx", s.Index.ToString());
                writer.WriteAttributeString("line", s.Line.Index.ToString());
                if (!s.HighTexture.Equals("-"))
                    writer.WriteAttributeString("high-tex", s.HighTexture);
                if (!s.LowTexture.Equals("-"))
                    writer.WriteAttributeString("low-tex", s.LowTexture);
                if (!s.MiddleTexture.Equals("-"))
                    writer.WriteAttributeString("middle-tex", s.MiddleTexture);
                writer.WriteAttributeString("offset-x", s.OffsetX.ToString());
                writer.WriteAttributeString("offset-y", s.OffsetY.ToString());
                writer.WriteAttributeString("front", s.IsFront.ToString());
                writeFields(s.Fields, writer);
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
        }

        void writeThings(MapSet map, XmlWriter writer)
        {
            writer.WriteStartElement("things");
            foreach (Thing t in map.Things)
            {
                writer.WriteStartElement("thing");
                writer.WriteAttributeString("idx", t.Index.ToString());
                writer.WriteAttributeString("tag", t.Tag.ToString());
                writer.WriteAttributeString("type", t.Type.ToString());
                writer.WriteAttributeString("action", t.Action.ToString());

                writer.WriteStartElement("pos");
                writer.WriteAttributeString("x", t.Position.x.ToString());
                writer.WriteAttributeString("y", t.Position.y.ToString());
                writer.WriteAttributeString("z", t.Position.z.ToString());
                writer.WriteAttributeString("angle-float", t.Angle.ToString());
                writer.WriteAttributeString("angle-int", t.AngleDoom.ToString());
                writer.WriteEndElement();

                writer.WriteStartElement("flags");
                foreach (String str in t.GetFlags().Keys)
                {
                    writer.WriteStartElement("flag");
                    writer.WriteAttributeString("name", str);
                    writer.WriteAttributeString("value", t.GetFlags()[str].ToString());
                    writer.WriteEndElement();
                }
                writer.WriteEndElement();

                writeFields(t.Fields, writer);

                writer.WriteEndElement();
            }
            writer.WriteEndElement();
        }

        public void write(string mapName, MapSet map, XmlWriter writer)
        {
            writer.WriteStartElement("map");
            writer.WriteAttributeString("name", mapName);

            writeSectors(map, writer);
            writeVertices(map, writer);
            writeLines(map, writer);
            writeSides(map, writer);
            writeThings(map, writer);

            writer.WriteEndElement();
        }

        public static void writeVertex(XmlWriter writer, Vector2D vert)
        {
            writer.WriteStartElement("vertex");
            writer.WriteAttributeString("x", vert.x.ToString());
            writer.WriteAttributeString("y", vert.y.ToString());
            writer.WriteEndElement();
        }
    }
}
