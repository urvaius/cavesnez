using System;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Nez.Content;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using System.Diagnostics;


namespace Nez.Tiled
{
	public class TiledMapReader : ContentTypeReader<TiledMap>
	{
		protected override TiledMap Read( ContentReader reader, TiledMap existingInstance )
		{
			var backgroundColor = reader.ReadColor();
			var renderOrder = (TiledRenderOrder)Enum.Parse( typeof( TiledRenderOrder ), reader.ReadString(), true );
			var tiledMap = new TiledMap( firstGid: reader.ReadInt32(),
										 width: reader.ReadInt32(),
				                         height: reader.ReadInt32(),
				                         tileWidth: reader.ReadInt32(),
				                         tileHeight: reader.ReadInt32(),
				                         orientation: (TiledMapOrientation)reader.ReadInt32() )
			{
				backgroundColor = backgroundColor,
				renderOrder = renderOrder
			};

			readCustomProperties( reader, tiledMap.properties );

			var tilesetCount = reader.ReadInt32();
			for( var i = 0; i < tilesetCount; i++ )
			{
				var isStandardTileset = reader.ReadBoolean();

				// image collections will have not texture associated with them
				var textureName = reader.ReadString();
				Texture2D texture = null;
				if( textureName != string.Empty )
				{
					var textureAssetName = reader.getRelativeAssetPath( textureName );
					texture = reader.ContentManager.Load<Texture2D>( textureAssetName );
				}

				var tileset = tiledMap.createTileset(
					                          texture: texture,
					                          firstId: reader.ReadInt32(),
					                          tileWidth: reader.ReadInt32(),
					                          tileHeight: reader.ReadInt32(),
											  isStandardTileset: isStandardTileset,
					                          spacing: reader.ReadInt32(),
					                          margin: reader.ReadInt32() );
				readCustomProperties( reader, tileset.properties );

				// tiledset tile array
				var tileCount = reader.ReadInt32();
				for( var j = 0; j < tileCount; j++ )
				{
					var tile = new TiledTilesetTile( reader.ReadInt32() );

					var tileAnimationFrameCount = reader.ReadInt32();
					if( tileAnimationFrameCount > 0 )
						tile.animationFrames = new List<TiledTileAnimationFrame>( tileAnimationFrameCount );
					
					for( var k = 0; k < tileAnimationFrameCount; k++ )
						tile.animationFrames.Add( new TiledTileAnimationFrame( reader.ReadInt32(), reader.ReadSingle() ) );

					// image source is optional
					if( reader.ReadBoolean() )
					{
						var rect = new Rectangle( reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32() );
						( (TiledImageCollectionTileset)tileset ).setTileTextureRegion( tileset.firstId + tile.id, rect );
					}

					readCustomProperties( reader, tile.properties );
					tileset.tiles.Add( tile );
				}
			}

			var layerCount = reader.ReadInt32();
			for( var i = 0; i < layerCount; i++ )
			{
				var layer = readLayer( reader, tiledMap );
				readCustomProperties( reader, layer.properties );
			}


			var objectGroupCount = reader.ReadInt32();
			for( var i = 0; i < objectGroupCount; i++ )
				readObjectGroup( reader, tiledMap );

			return tiledMap;
		}


		private static void readCustomProperties( ContentReader reader, Dictionary<string,string> properties )
		{
			var count = reader.ReadInt32();
			for( var i = 0; i < count; i++ )
				properties.Add( reader.ReadString(), reader.ReadString() );
		}


		private static TiledLayer readLayer( ContentReader reader, TiledMap tiledMap )
		{
			var layerName = reader.ReadString();
			var visible = reader.ReadBoolean();
			var opacity = reader.ReadSingle();
			var offset = reader.ReadVector2();
			var layerType = (TiledLayerType)reader.ReadInt32();

			TiledLayer layer;
			if( layerType == TiledLayerType.Tile )
				layer = readTileLayer( reader, tiledMap, layerName );
			else if( layerType == TiledLayerType.Image )
				layer = readImageLayer( reader, tiledMap, layerName );
			else
				throw new NotSupportedException( string.Format( "Layer type {0} with name {1} is not supported", layerType, layerName ) );

			layer.offset = offset;
			layer.visible = visible;
			layer.opacity = opacity;

			return layer;
		}


		private static TiledTileLayer readTileLayer( ContentReader reader, TiledMap tileMap, string layerName )
		{
			var tileCount = reader.ReadInt32();
			var tiles = new TiledTile[tileCount];

			for( var i = 0; i < tileCount; i++ )
			{
				var tileId = reader.ReadInt32();
				var flippedHorizonally = reader.ReadBoolean();
				var flippedVertically = reader.ReadBoolean();
				var flippedDiagonally = reader.ReadBoolean();

				// dont add null tiles
				if( tileId != 0 )
				{
					var tilesetTile = tileMap.getTilesetTile( tileId );
					if( tilesetTile != null && tilesetTile.animationFrames != null )
					{
						if( tilesetTile.animationFrames.Count > 0 )
						{
							tiles[i] = new TiledAnimatedTile( tileId, tilesetTile ) {
								flippedHorizonally = flippedHorizonally,
								flippedVertically = flippedVertically,
								flippedDiagonally = flippedDiagonally
							};
							tileMap._animatedTiles.Add( tiles[i] as TiledAnimatedTile );
						}
					}
					else
					{
						tiles[i] = new TiledTile( tileId )
						{
							flippedHorizonally = flippedHorizonally,
							flippedVertically = flippedVertically,
							flippedDiagonally = flippedDiagonally
						};
					}

					tiles[i].tileset = tileMap.getTilesetForTileId( tileId );
				}
				else
				{
					tiles[i] = null;
				}
			}

			return tileMap.createTileLayer(
				name: layerName,
				width: reader.ReadInt32(),
				height: reader.ReadInt32(),
				tiles: tiles );
		}


		private static TiledImageLayer readImageLayer( ContentReader reader, TiledMap tileMap, string layerName )
		{
			var assetName = reader.getRelativeAssetPath( reader.ReadString() );
			var texture = reader.ContentManager.Load<Texture2D>( assetName );

			return tileMap.createImageLayer( layerName, texture );
		}


		private static TiledObjectGroup readObjectGroup( ContentReader reader, TiledMap tiledMap )
		{
			var objectGroup = tiledMap.createObjectGroup(
				reader.ReadString(), reader.ReadColor(), reader.ReadBoolean(), reader.ReadSingle() );

			readCustomProperties( reader, objectGroup.properties );

			var objectCount = reader.ReadInt32();
			objectGroup.objects = new TiledObject[objectCount];
			for( var i = 0; i < objectCount; i++ )
			{
				var obj = new TiledObject()
				{
					gid = reader.ReadInt32(),
					name = reader.ReadString(),
					type = reader.ReadString(),
					x = reader.ReadInt32(),
					y = reader.ReadInt32(),
					width = reader.ReadInt32(),
					height = reader.ReadInt32(),
					rotation = reader.ReadInt32(),
					visible = reader.ReadBoolean()
				};
						
				var tiledObjectType = reader.ReadString();
				if( tiledObjectType == "ellipse" )
				{
					// ellipse has no extra props
					obj.tiledObjectType = TiledObject.TiledObjectType.Ellipse;
				}
				else if( tiledObjectType == "image" )
				{
					obj.tiledObjectType = TiledObject.TiledObjectType.Image;
					throw new NotImplementedException( "Image objects are not yet implemented" );
				}
				else if( tiledObjectType == "polygon" )
				{
					obj.tiledObjectType = TiledObject.TiledObjectType.Polygon;
					obj.polyPoints = readVector2Array( reader );
				}
				else if( tiledObjectType == "polyline" )
				{
					obj.tiledObjectType = TiledObject.TiledObjectType.Polyline;
					obj.polyPoints = readVector2Array( reader );
				}
				else
				{
					obj.tiledObjectType = TiledObject.TiledObjectType.None;
				}

				obj.objectType = reader.ReadString();

				readCustomProperties( reader, obj.properties );

				objectGroup.objects[i] = obj;
			}

			return objectGroup;
		}


		static Vector2[] readVector2Array( ContentReader reader )
		{
			var pointCount = reader.ReadInt32();
			var points = new Vector2[pointCount];

			for( var i = 0; i < pointCount; i++ )
				points[i] = reader.ReadVector2();

			return points;
		}
	
	}
}