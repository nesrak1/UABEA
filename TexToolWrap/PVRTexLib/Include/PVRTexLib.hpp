/*!***********************************************************************
 @file         PVRTexLib.hpp
 @copyright    Copyright (c) Imagination Technologies Limited.
 @brief        C++ wrapper around PVRTexLib's C interface.
*************************************************************************/
#pragma once

#include "PVRTexLib.h"
#include <string>
#include <array>
#include <memory>

namespace pvrtexlib
{
	struct MetaDataBlock
	{
		PVRTuint32	DevFOURCC;				///< A 4cc descriptor of the data type's creator. Values equating to values between 'P' 'V' 'R' 0 and 'P' 'V' 'R' 255 will be used by our headers.
		PVRTuint32	u32Key;					///< A DWORD (enum value) identifying the data type, and thus how to read it.
		PVRTuint32	u32DataSize;			///< Size of the Data member.
		std::unique_ptr<PVRTuint8[]> Data;	///< Meta data bytes

		MetaDataBlock()
			: DevFOURCC(PVRTEX_CURR_IDENT)
			, u32Key()
			, u32DataSize()
			, Data()
		{}
	};

	class PVRTextureHeader
	{
	public:
		/*!***********************************************************************
		 @brief     Creates a new texture header with default parameters.
		 @param[in]	params PVRHeader_CreateParams
		 @return	A new PVRTextureHeader
		*************************************************************************/
		PVRTextureHeader()
			: m_hTextureHeader()
		{
			PVRHeader_CreateParams params;
			PVRTexLib_SetDefaultTextureHeaderParams(&params);
			m_hTextureHeader = PVRTexLib_CreateTextureHeader(&params);
		}

		/*!***********************************************************************
		 @brief     Creates a new texture header using the supplied parameters.
		 @param[in]	params PVRHeader_CreateParams
		 @return	A new PVRTextureHeader
		*************************************************************************/
		PVRTextureHeader(const PVRHeader_CreateParams* params)
			: m_hTextureHeader(PVRTexLib_CreateTextureHeader(params))
		{
		}

		/*!***********************************************************************
		 @brief     Creates a new texture header using the supplied parameters.
		 @param[in]	pixelFormat texture format
		 @param[in]	width texture width in pixels
		 @param[in]	height texture height in pixels
		 @param[in]	depth texture depth
		 @param[in]	numMipMaps number of MIP map levels
		 @param[in]	numArrayMembers number of array members
		 @param[in]	numFaces number of faces
		 @param[in]	colourSpace colour space
		 @param[in]	channelType channel type
		 @param[in]	preMultiplied texture's colour has been pre-multiplied by the alpha values?
		 @return	A new PVRTextureHeader
		*************************************************************************/
		PVRTextureHeader(
			PVRTuint64				pixelFormat,
			PVRTuint32				width,
			PVRTuint32				height,
			PVRTuint32				depth = 1U,
			PVRTuint32				numMipMaps = 1U,
			PVRTuint32				numArrayMembers = 1U,
			PVRTuint32				numFaces = 1U,
			PVRTexLibColourSpace	colourSpace = PVRTexLibColourSpace::PVRTLCS_sRGB,
			PVRTexLibVariableType	channelType = PVRTexLibVariableType::PVRTLVT_UnsignedByteNorm,
			bool					preMultiplied = false)
			: m_hTextureHeader()
		{
			PVRHeader_CreateParams params;
			params.pixelFormat = pixelFormat;
			params.width = width;
			params.height = height;
			params.depth = depth;
			params.numMipMaps = numMipMaps;
			params.numArrayMembers = numArrayMembers;
			params.numFaces = numFaces;
			params.colourSpace = colourSpace;
			params.channelType = channelType;
			params.preMultiplied = preMultiplied;
			m_hTextureHeader = PVRTexLib_CreateTextureHeader(&params);
		}

		/*!***********************************************************************
		 @brief		Creates a new texture header from a PVRTextureHeader
		 @param[in]	rhs Texture header to copy
		 @return	A new PVRTextureHeader
		*************************************************************************/
		PVRTextureHeader(const PVRTextureHeader& rhs)
			: m_hTextureHeader(PVRTexLib_CopyTextureHeader(rhs.m_hTextureHeader))
		{
		}

		/*!***********************************************************************
		 @brief     Creates a new texture, moving the contents of the
					supplied texture into the new texture.
		 @param[in]	texture A PVRTextureHeader to move from.
		 @return	A new PVRTextureHeader.
		*************************************************************************/
		PVRTextureHeader(PVRTextureHeader&& rhs)
			: m_hTextureHeader(rhs.m_hTextureHeader)
		{
			rhs.m_hTextureHeader = nullptr;
		}

		/*!***********************************************************************
		 @brief     Copies the contents of another texture header into this one.
		 @param[in]	rhs PVRTextureHeader to copy
		 @return	This texture header.
		*************************************************************************/
		PVRTextureHeader& operator=(const PVRTextureHeader& rhs)
		{
			if (&rhs == this)
				return *this;

			if (m_hTextureHeader)
			{
				PVRTexLib_DestroyTextureHeader(m_hTextureHeader);
				m_hTextureHeader = nullptr;
			}

			m_hTextureHeader = PVRTexLib_CopyTextureHeader(rhs.m_hTextureHeader);
			return *this;
		}

		/*!***********************************************************************
		 @brief     Moves ownership of texture header data to this object.
		 @param[in]	rhs PVRTextureHeader to move
		 @return	This texture header.
		*************************************************************************/
		PVRTextureHeader& operator=(PVRTextureHeader&& rhs)
		{
			if (&rhs == this)
				return *this;

			if (m_hTextureHeader)
			{
				PVRTexLib_DestroyTextureHeader(m_hTextureHeader);
				m_hTextureHeader = nullptr;
			}

			m_hTextureHeader = rhs.m_hTextureHeader;
			rhs.m_hTextureHeader = nullptr;
			return *this;
		}

		virtual ~PVRTextureHeader()
		{
			if (m_hTextureHeader)
			{
				PVRTexLib_DestroyTextureHeader(m_hTextureHeader);
				m_hTextureHeader = nullptr;
			}			
		}

		/*!***********************************************************************
		 @brief		Gets the number of bits per pixel for this texture header.
		 @return	Number of bits per pixel.
		*************************************************************************/
		PVRTuint32 GetTextureBitsPerPixel() const
		{
			if (m_hTextureHeader)
			{
				return PVRTexLib_GetTextureBitsPerPixel(m_hTextureHeader);
			}
			else
			{
				return 0U;
			}
		}

		/*!***********************************************************************
		 @brief		Gets the number of bits per pixel for the specified pixel format.
		 @param		u64PixelFormat A PVR pixel format ID.
		 @return	Number of bits per pixel.
		*************************************************************************/
		static PVRTuint32 GetTextureBitsPerPixel(PVRTuint64 u64PixelFormat)
		{
			return PVRTexLib_GetFormatBitsPerPixel(u64PixelFormat);
		}

		/*!***********************************************************************
		 @brief		Gets the number of channels for this texture header.
		 @return	For uncompressed formats the number of channels between 1 and 4.
					For compressed formats 0
		*************************************************************************/
		PVRTuint32 GetTextureChannelCount() const
		{
			if (m_hTextureHeader)
			{
				return PVRTexLib_GetTextureChannelCount(m_hTextureHeader);
			}
			else
			{
				return 0U;
			}
		}

		/*!***********************************************************************
		 @brief		Gets the channel type for this texture header.
		 @return	PVRTexLibVariableType enum.
		*************************************************************************/
		PVRTexLibVariableType GetTextureChannelType() const
		{
			if (m_hTextureHeader)
			{
				return PVRTexLib_GetTextureChannelType(m_hTextureHeader);
			}
			else
			{
				return PVRTexLibVariableType::PVRTLVT_Invalid;
			}
		}

		/*!***********************************************************************
		 @brief		Gets the colour space for this texture header.
		 @return	PVRTexLibColourSpace enum.
		*************************************************************************/
		PVRTexLibColourSpace GetColourSpace() const
		{
			if (m_hTextureHeader)
			{
				return PVRTexLib_GetTextureColourSpace(m_hTextureHeader);
			}
			else
			{
				return PVRTexLibColourSpace::PVRTLCS_NumSpaces;
			}
		}

		/*!***********************************************************************
		 @brief     Gets the width of the user specified MIP-Map level for the
					texture
		 @param[in]	uiMipLevel MIP level that user is interested in.
		 @return    Width of the specified MIP-Map level.
		*************************************************************************/
		PVRTuint32 GetTextureWidth(PVRTuint32 mipLevel) const
		{
			if (m_hTextureHeader)
			{
				return PVRTexLib_GetTextureWidth(m_hTextureHeader, mipLevel);
			}
			else
			{
				return 0U;
			}
		}

		/*!***********************************************************************
		 @brief     Gets the height of the user specified MIP-Map
					level for the texture
		 @param[in]	uiMipLevel MIP level that user is interested in.
		 @return	Height of the specified MIP-Map level.
		*************************************************************************/
		PVRTuint32 GetTextureHeight(PVRTuint32 mipLevel) const
		{
			if (m_hTextureHeader)
			{
				return PVRTexLib_GetTextureHeight(m_hTextureHeader, mipLevel);
			}
			else
			{
				return 0U;
			}
		}

		/*!***********************************************************************
		 @brief     Gets the depth of the user specified MIP-Map
					level for the texture
		 @param[in]	uiMipLevel MIP level that user is interested in.
		 @return	Depth of the specified MIP-Map level.
		*************************************************************************/
		PVRTuint32 GetTextureDepth(PVRTuint32 mipLevel) const
		{
			if (m_hTextureHeader)
			{
				return PVRTexLib_GetTextureDepth(m_hTextureHeader, mipLevel);
			}
			else
			{
				return 0U;
			}
		}

		/*!***********************************************************************
		 @brief     Gets the size in PIXELS of the texture, given various input
					parameters.	User can retrieve the total size of either all
					surfaces or a single surface, all faces or a single face and
					all MIP-Maps or a single specified MIP level.
		 @param[in]	iMipLevel		Specifies a MIP level to check,
									'PVRTEX_ALLMIPLEVELS' can be passed to get
									the size of all MIP levels.
		 @param[in]	bAllSurfaces	Size of all surfaces is calculated if true,
									only a single surface if false.
		 @param[in]	bAllFaces		Size of all faces is calculated if true,
									only a single face if false.
		 @return	Size in PIXELS of the specified texture area.
		*************************************************************************/
		PVRTuint32 GetTextureSize(PVRTint32 mipLevel = PVRTEX_ALLMIPLEVELS, bool allSurfaces = true, bool allFaces = true) const
		{
			if (m_hTextureHeader)
			{
				return PVRTexLib_GetTextureSize(m_hTextureHeader, mipLevel, allSurfaces, allFaces);
			}
			else
			{
				return 0U;
			}
		}

		/*!***********************************************************************
		 @brief		Gets the size in BYTES of the texture, given various input
					parameters.	User can retrieve the size of either all
					surfaces or a single surface, all faces or a single face
					and all MIP-Maps or a single specified MIP level.
		 @param[in]	iMipLevel		Specifies a mip level to check,
									'PVRTEX_ALLMIPLEVELS' can be passed to get
									the size of all MIP levels.
		 @param[in]	bAllSurfaces	Size of all surfaces is calculated if true,
									only a single surface if false.
		 @param[in]	bAllFaces		Size of all faces is calculated if true,
									only a single face if false.
		 @return	Size in BYTES of the specified texture area.
		*************************************************************************/
		PVRTuint32 GetTextureDataSize(PVRTint32 mipLevel = PVRTEX_ALLMIPLEVELS, bool allSurfaces = true, bool allFaces = true) const
		{
			if (m_hTextureHeader)
			{
				return PVRTexLib_GetTextureDataSize(m_hTextureHeader, mipLevel, allSurfaces, allFaces);
			}
			else
			{
				return 0U;
			}
		}

		/*!***********************************************************************
		 @brief      	Gets the data orientation for this texture.
		 @param[in,out]	result Pointer to a PVRTexLib_Orientation structure.
		*************************************************************************/
		void GetTextureOrientation(PVRTexLib_Orientation& result) const
		{
			if (m_hTextureHeader)
			{
				PVRTexLib_GetTextureOrientation(m_hTextureHeader, &result);
			}
			else
			{
				result.x = result.y = result.z = (PVRTexLibOrientation)0U;
			}
		}

		/*!***********************************************************************
		 @brief      	Gets the OpenGL equivalent format for this texture.
		 @param[in,out]	result Pointer to a PVRTexLib_OpenGLFormat structure.
		*************************************************************************/
		void GetTextureOpenGLFormat(PVRTexLib_OpenGLFormat& result) const
		{
			if (m_hTextureHeader)
			{
				PVRTexLib_GetTextureOpenGLFormat(m_hTextureHeader, &result);
			}
			else
			{
				result.internalFormat = result.format = result.type = 0U;
			}
		}

		/*!***********************************************************************
		 @brief      	Gets the OpenGLES equivalent format for this texture.
		 @param[in,out]	result Pointer to a PVRTexLib_OpenGLESFormat structure.
		*************************************************************************/
		void GetTextureOpenGLESFormat(PVRTexLib_OpenGLESFormat& result) const
		{
			if (m_hTextureHeader)
			{
				PVRTexLib_GetTextureOpenGLESFormat(m_hTextureHeader, &result);
			}
			else
			{
				result.internalFormat = result.format = result.type = 0U;
			}
		}

		/*!***********************************************************************
		 @brief     Gets the Vulkan equivalent format for this texture.
		 @return	A VkFormat enum value.
		*************************************************************************/
		PVRTuint32 GetTextureVulkanFormat() const
		{
			if (m_hTextureHeader)
			{
				return PVRTexLib_GetTextureVulkanFormat(m_hTextureHeader);
			}
			else
			{
				return 0U;
			}
		}

		/*!***********************************************************************
		 @brief     Gets the Direct3D equivalent format for this texture.
		 @return	A D3DFORMAT enum value.
		*************************************************************************/
		PVRTuint32 GetTextureD3DFormat() const
		{
			if (m_hTextureHeader)
			{
				return PVRTexLib_GetTextureD3DFormat(m_hTextureHeader);
			}
			else
			{
				return 0U;
			}
		}

		/*!***********************************************************************
		 @brief     Gets the DXGI equivalent format for this texture.
		 @return	A DXGI_FORMAT enum value.
		*************************************************************************/
		PVRTuint32 GetTextureDXGIFormat() const
		{
			if (m_hTextureHeader)
			{
				return PVRTexLib_GetTextureDXGIFormat(m_hTextureHeader);
			}
			else
			{
				return 0U;
			}
		}

		/*!***********************************************************************
		 @brief 				Gets the minimum dimensions (x,y,z)
								for the textures pixel format.
		 @param[in,out]	minX	Returns the minimum width.
		 @param[in,out]	minY	Returns the minimum height.
		 @param[in,out]	minZ	Returns the minimum depth.
		*************************************************************************/
		void GetTextureFormatMinDims(PVRTuint32& minX, PVRTuint32& minY, PVRTuint32& minZ) const
		{
			if (m_hTextureHeader)
			{
				return PVRTexLib_GetTextureFormatMinDims(m_hTextureHeader, &minX, &minY, &minZ);
			}
			else
			{
				minX = minY = minZ = 1U;
			}
		}

		/*!***********************************************************************
		 @brief 		Gets the minimum dimensions (x,y,z)
						for a given pixel format.
		 @param[in]		u64PixelFormat A PVR Pixel Format ID.
		 @param[in,out]	minX Returns the minimum width.
		 @param[in,out]	minY Returns the minimum height.
		 @param[in,out]	minZ Returns the minimum depth.
		*************************************************************************/
		static void GetPixelFormatMinDims(PVRTuint64 ui64Format, PVRTuint32& minX, PVRTuint32& minY, PVRTuint32& minZ)
		{
			PVRTexLib_GetPixelFormatMinDims(ui64Format, &minX, &minY, &minZ);
		}

		/*!***********************************************************************
		 @brief		Returns the total size of the meta data stored in the header.
					This includes the size of all information stored in all MetaDataBlocks.
		 @return	Size, in bytes, of the meta data stored in the header.
		*************************************************************************/
		PVRTuint32 GetTextureMetaDataSize() const
		{
			if (m_hTextureHeader)
			{
				return PVRTexLib_GetTextureMetaDataSize(m_hTextureHeader);
			}
			else
			{
				return 0U;
			}
		}

		/*!***********************************************************************
		 @brief		Returns whether or not the texture's colour has been
					pre-multiplied by the alpha values.
		 @return	True if texture is premultiplied.
		*************************************************************************/
		bool GetTextureIsPreMultiplied() const
		{
			if (m_hTextureHeader)
			{
				return PVRTexLib_GetTextureIsPreMultiplied(m_hTextureHeader);
			}
			else
			{
				return false;
			}
		}

		/*!***********************************************************************
		 @brief		Returns whether or not the texture is compressed using
					PVRTexLib's FILE compression - this is independent of
					any texture compression.
		 @return	True if it is file compressed.
		*************************************************************************/
		bool GetTextureIsFileCompressed() const
		{
			if (m_hTextureHeader)
			{
				return PVRTexLib_GetTextureIsFileCompressed(m_hTextureHeader);
			}
			else
			{
				return false;
			}
		}

		/*!***********************************************************************
		 @brief		Returns whether or not the texture is a bump map.
		 @return	True if it is a bump map.
		*************************************************************************/
		bool GetTextureIsBumpMap() const
		{
			if (m_hTextureHeader)
			{
				return PVRTexLib_GetTextureIsBumpMap(m_hTextureHeader);
			}
			else
			{
				return false;
			}
		}

		/*!***********************************************************************
		 @brief     Gets the bump map scaling value for this texture.
					If the texture is not a bump map, 0.0f is returned. If the
					texture is a bump map but no meta data is stored to
					specify its scale, then 1.0f is returned.
		 @return	Returns the bump map scale value as a float.
		*************************************************************************/
		float GetTextureBumpMapScale() const
		{
			if (m_hTextureHeader)
			{
				return PVRTexLib_GetTextureBumpMapScale(m_hTextureHeader);
			}
			else
			{
				return 0.0f;
			}
		}

		/*!***********************************************************************
		 @brief     Works out the number of possible texture atlas members in
					the texture based on the width, height, depth and data size.
		 @return	The number of sub textures defined by meta data.
		*************************************************************************/
		PVRTuint32 GetNumTextureAtlasMembers() const
		{
			if (m_hTextureHeader)
			{
				return PVRTexLib_GetNumTextureAtlasMembers(m_hTextureHeader);
			}
			else
			{
				return 0U;
			}
		}

		/*!***********************************************************************
		 @brief			Returns a pointer to the texture atlas data.
		 @param[in,out]	count Number of floats in the returned data set.
		 @return		A pointer directly to the texture atlas data. NULL if
						the texture does not have atlas data.
		*************************************************************************/
		const float* GetTextureAtlasData(PVRTuint32& count) const
		{
			if (m_hTextureHeader)
			{
				return PVRTexLib_GetTextureAtlasData(m_hTextureHeader, &count);
			}
			else
			{
				count = 0U;
				return nullptr;
			}
		}

		/*!***********************************************************************
		 @brief     Gets the number of MIP-Map levels stored in this texture.
		 @return	Number of MIP-Map levels in this texture.
		*************************************************************************/
		PVRTuint32 GetTextureNumMipMapLevels() const
		{
			if (m_hTextureHeader)
			{
				return PVRTexLib_GetTextureNumMipMapLevels(m_hTextureHeader);
			}
			else
			{
				return 0U;
			}
		}

		/*!***********************************************************************
		 @brief     Gets the number of faces stored in this texture.
		 @return	Number of faces in this texture.
		*************************************************************************/
		PVRTuint32 GetTextureNumFaces() const
		{
			if (m_hTextureHeader)
			{
				return PVRTexLib_GetTextureNumFaces(m_hTextureHeader);
			}
			else
			{
				return 0U;
			}
		}

		/*!***********************************************************************
		 @brief     Gets the number of array members stored in this texture.
		 @return	Number of array members in this texture.
		*************************************************************************/
		PVRTuint32 GetTextureNumArrayMembers() const
		{
			if (m_hTextureHeader)
			{
				return PVRTexLib_GetTextureNumArrayMembers(m_hTextureHeader);
			}
			else
			{
				return 0U;
			}
		}

		/*!***********************************************************************
		 @brief		Gets the cube map face order.
					cubeOrder string will be in the form "ZzXxYy" with capitals
					representing positive and lower case letters representing
					negative. I.e. Z=Z-Positive, z=Z-Negative.
		 @return	Null terminated cube map order string.
		*************************************************************************/
		std::array<char, 7> GetTextureCubeMapOrder() const
		{
			std::array<char, 7> cubeOrder = { 0 };
			if (m_hTextureHeader)
			{
				PVRTexLib_GetTextureCubeMapOrder(m_hTextureHeader, cubeOrder.data());
			}

			return cubeOrder;
		}

		/*!***********************************************************************
		 @brief     Gets the bump map channel order relative to rgba.
					For	example, an RGB texture with bumps mapped to XYZ returns
					'xyz'. A BGR texture with bumps in the order ZYX will also
					return 'xyz' as the mapping is the same: R=X, G=Y, B=Z.
					If the letter 'h' is present in the string, it means that
					the height map has been stored here.
					Other characters are possible if the bump map was created
					manually, but PVRTexLib will ignore these characters. They
					are returned simply for completeness.
		 @return	Null terminated bump map order string relative to rgba.
		*************************************************************************/
		std::array<char, 5> GetTextureBumpMapOrder() const
		{
			std::array<char, 5> bumpOrder = { 0 };
			if (m_hTextureHeader)
			{
				PVRTexLib_GetTextureBumpMapOrder(m_hTextureHeader, bumpOrder.data());
			}

			return bumpOrder;
		}

		/*!***********************************************************************
		 @brief     Gets the 64-bit pixel type ID of the texture.
		 @return	64-bit pixel type ID.
		*************************************************************************/
		PVRTuint64 GetTexturePixelFormat() const
		{
			if (m_hTextureHeader)
			{
				return PVRTexLib_GetTexturePixelFormat(m_hTextureHeader);
			}
			else
			{
				return (PVRTuint64)PVRTexLibPixelFormat::PVRTLPF_NumCompressedPFs;
			}
		}

		/*!***********************************************************************
		 @brief     Checks whether the pixel format of the texture is packed.
					E.g. R5G6B5, R11G11B10, R4G4B4A4 etc.
		 @return	True if the texture format is packed, false otherwise.
		*************************************************************************/
		bool TextureHasPackedChannelData() const
		{
			if (m_hTextureHeader)
			{
				return PVRTexLib_TextureHasPackedChannelData(m_hTextureHeader);
			}
			else
			{
				return false;
			}
		}

		/*!***********************************************************************
		 @brief		Sets the variable type for the channels in this texture.
		 @param[in]	type A PVRTexLibVariableType enum.
		*************************************************************************/
		void SetTextureChannelType(PVRTexLibVariableType type)
		{
			if (m_hTextureHeader)
			{
				PVRTexLib_SetTextureChannelType(m_hTextureHeader, type);
			}
		}

		/*!***********************************************************************
		 @brief     Sets the colour space for this texture.
		 @param[in]	colourSpace	A PVRTexLibColourSpace enum.
		*************************************************************************/
		void SetTextureColourSpace(PVRTexLibColourSpace colourSpace)
		{
			if (m_hTextureHeader)
			{
				PVRTexLib_SetTextureColourSpace(m_hTextureHeader, colourSpace);
			}
		}

		/*!***********************************************************************
		 @brief     Sets the format of the texture to PVRTexLib's internal
					representation of the D3D format.
		 @param[in] d3dFormat A D3DFORMAT enum.
		 @return	True if successful.
		*************************************************************************/
		bool SetTextureD3DFormat(PVRTuint32 d3dFormat)
		{
			if (m_hTextureHeader)
			{
				return PVRTexLib_SetTextureD3DFormat(m_hTextureHeader, d3dFormat);
			}
			else
			{
				return false;
			}
		}

		/*!***********************************************************************
		 @brief     Sets the format of the texture to PVRTexLib's internal
					representation of the DXGI format.
		 @param[in]	dxgiFormat A DXGI_FORMAT enum.
		 @return	True if successful.
		*************************************************************************/
		bool SetTextureDXGIFormat(PVRTuint32 dxgiFormat)
		{
			if (m_hTextureHeader)
			{
				return PVRTexLib_SetTextureDXGIFormat(m_hTextureHeader, dxgiFormat);
			}
			else
			{
				return false;
			}
		}

		/*!***********************************************************************
		 @brief		Sets the format of the texture to PVRTexLib's internal
					representation of the OpenGL format.
		 @param[in]	oglFormat The OpenGL format.
		 @return	True if successful.
		*************************************************************************/
		bool SetTextureOGLFormat(const PVRTexLib_OpenGLFormat& oglFormat)
		{
			if (m_hTextureHeader)
			{
				return PVRTexLib_SetTextureOGLFormat(m_hTextureHeader, &oglFormat);
			}
			else
			{
				return false;
			}
		}

		/*!***********************************************************************
		 @brief		Sets the format of the texture to PVRTexLib's internal
					representation of the OpenGLES format.
		 @param[in]	oglesFormat The OpenGLES format.
		 @return	True if successful.
		*************************************************************************/
		bool SetTextureOGLESFormat(const PVRTexLib_OpenGLESFormat& oglesFormat)
		{
			if (m_hTextureHeader)
			{
				return PVRTexLib_SetTextureOGLESFormat(m_hTextureHeader, &oglesFormat);
			}
			else
			{
				return false;
			}
		}

		/*!***********************************************************************
		 @brief		Sets the format of the texture to PVRTexLib's internal
					representation of the Vulkan format.
		 @param[in]	vulkanFormat A VkFormat enum.
		 @return	True if successful.
		*************************************************************************/
		bool SetTextureVulkanFormat(PVRTuint32 vulkanFormat)
		{
			if (m_hTextureHeader)
			{
				return PVRTexLib_SetTextureVulkanFormat(m_hTextureHeader, vulkanFormat);
			}
			else
			{
				return false;
			}
		}

		/*!***********************************************************************
		 @brief     Sets the pixel format for this texture.
		 @param[in]	format	The format of the pixel.
		*************************************************************************/
		void SetTexturePixelFormat(PVRTuint64 format)
		{
			if (m_hTextureHeader)
			{
				PVRTexLib_SetTexturePixelFormat(m_hTextureHeader, format);
			}
		}

		/*!***********************************************************************
		 @brief		Sets the texture width.
		 @param[in]	width The new width.
		*************************************************************************/
		void SetTextureWidth(PVRTuint32 width)
		{
			if (m_hTextureHeader)
			{
				PVRTexLib_SetTextureWidth(m_hTextureHeader, width);
			}
		}

		/*!***********************************************************************
		 @brief		Sets the texture height.
		 @param[in]	height The new height.
		*************************************************************************/
		void SetTextureHeight(PVRTuint32 height)
		{
			if (m_hTextureHeader)
			{
				PVRTexLib_SetTextureHeight(m_hTextureHeader, height);
			}
		}

		/*!***********************************************************************
		 @brief		Sets the texture depth.
		 @param[in]	depth The new depth.
		*************************************************************************/
		void SetTextureDepth(PVRTuint32 depth)
		{
			if (m_hTextureHeader)
			{
				PVRTexLib_SetTextureDepth(m_hTextureHeader, depth);
			}
		}

		/*!***********************************************************************
		 @brief		Sets the number of array members in this texture.
		 @param[in]	newNumMembers The new number of array members.
		*************************************************************************/
		void SetTextureNumArrayMembers(PVRTuint32 numMembers)
		{
			if (m_hTextureHeader)
			{
				PVRTexLib_SetTextureNumArrayMembers(m_hTextureHeader, numMembers);
			}
		}

		/*!***********************************************************************
		 @brief		Sets the number of MIP-Map levels in this texture.
		 @param[in]	numMIPLevels New number of MIP-Map levels.
		*************************************************************************/
		void SetTextureNumMIPLevels(PVRTuint32 numMIPLevels)
		{
			if (m_hTextureHeader)
			{
				PVRTexLib_SetTextureNumMIPLevels(m_hTextureHeader, numMIPLevels);
			}
		}

		/*!***********************************************************************
		 @brief		Sets the number of faces stored in this texture.
		 @param[in]	numFaces New number of faces for this texture.
		*************************************************************************/
		void SetTextureNumFaces(PVRTuint32 numFaces)
		{
			if (m_hTextureHeader)
			{
				PVRTexLib_SetTextureNumFaces(m_hTextureHeader, numFaces);
			}
		}

		/*!***********************************************************************
		 @brief     Sets the data orientation for a given axis in this texture.
		 @param[in]	orientation Pointer to a PVRTexLib_Orientation struct.
		*************************************************************************/
		void SetTextureOrientation(const PVRTexLib_Orientation& orientation)
		{
			if (m_hTextureHeader)
			{
				PVRTexLib_SetTextureOrientation(m_hTextureHeader, &orientation);
			}
		}

		/*!***********************************************************************
		 @brief		Sets whether or not the texture is compressed using
					PVRTexLib's FILE compression - this is independent of
					any texture compression. Currently unsupported.
		 @param[in]	isFileCompressed Sets the file compression to true or false.
		*************************************************************************/
		void SetTextureIsFileCompressed(bool isFileCompressed)
		{
			if (m_hTextureHeader)
			{
				PVRTexLib_SetTextureIsFileCompressed(m_hTextureHeader, isFileCompressed);
			}
		}

		/*!***********************************************************************
		 @brief     Sets whether or not the texture's colour has been
					pre-multiplied by the alpha values.
		 @param[in] isPreMultiplied	Sets if texture is premultiplied.
		*************************************************************************/
		void SetTextureIsPreMultiplied(bool isPreMultiplied)
		{
			if (m_hTextureHeader)
			{
				PVRTexLib_SetTextureIsPreMultiplied(m_hTextureHeader, isPreMultiplied);
			}
		}

		/*!***********************************************************************
		 @brief			Obtains the border size in each dimension for this texture.
		 @param[in,out]	borderWidth   Border width
		 @param[in,out]	borderHeight  Border height
		 @param[in,out]	borderDepth   Border depth
		*************************************************************************/
		void GetTextureBorder(PVRTuint32& borderWidth, PVRTuint32& borderHeight, PVRTuint32& borderDepth) const
		{
			if (m_hTextureHeader)
			{
				PVRTexLib_GetTextureBorder(m_hTextureHeader, &borderWidth, &borderHeight, &borderDepth);
			}
			else
			{
				borderDepth = borderHeight = borderDepth = 0U;
			}
		}

		/*!***********************************************************************
		 @brief			Returns a copy of a block of meta data from the texture.
						If the meta data doesn't exist, a block with a data size
						of 0 will be returned.
		 @param[in]		key Value representing the type of meta data stored
		 @param[in,out] dataBlock returned meta block data
		 @param[in]		devFOURCC Four character descriptor representing the
						creator of the meta data
		 @return		True if the meta data block was found. False otherwise.
		*************************************************************************/
		bool GetMetaDataBlock(PVRTuint32 key, MetaDataBlock& dataBlock, PVRTuint32 devFOURCC = PVRTEX_CURR_IDENT) const
		{
			if (m_hTextureHeader)
			{
				PVRTexLib_MetaDataBlock tmp;
				if (PVRTexLib_GetMetaDataBlock(m_hTextureHeader, devFOURCC, key,
					&tmp, [](PVRTuint32 bytes) { return (void*)new PVRTuint8[bytes]; }))
				{
					dataBlock.DevFOURCC = tmp.DevFOURCC;
					dataBlock.u32Key = tmp.u32Key;
					dataBlock.u32DataSize = tmp.u32DataSize;
					dataBlock.Data.reset(tmp.Data);
					return true;
				}
				else
				{
					dataBlock = MetaDataBlock();
					return false;
				}
			}
			else
			{
				dataBlock = MetaDataBlock();
				return false;
			}
		}

		/*!***********************************************************************
		 @brief     Returns whether or not the specified meta data exists as
					part of this texture header.
		 @param[in]	u32Key Key value representing the type of meta data stored
		 @param[in]	DevFOURCC Four character descriptor representing the
							  creator of the meta data
		 @return	True if the specified meta data bock exists
		*************************************************************************/
		bool TextureHasMetaData(PVRTuint32 key, PVRTuint32 devFOURCC = PVRTEX_CURR_IDENT) const
		{
			if (m_hTextureHeader)
			{
				return PVRTexLib_TextureHasMetaData(m_hTextureHeader, devFOURCC, key);
			}
			else
			{
				return false;
			}
		}

		/*!***********************************************************************
		 @brief     Sets a texture's bump map data.
		 @param[in]	bumpScale Floating point "height" value to scale the bump map.
		 @param[in]	bumpOrder Up to 4 character string, with values x,y,z,h in
							  some combination. Not all values need to be present.
							  Denotes channel order; x,y,z refer to the
							  corresponding axes, h indicates presence of the
							  original height map. It is possible to have only some
							  of these values rather than all. For example if 'h'
							  is present alone it will be considered a height map.
							  The values should be presented in RGBA order, regardless
							  of the texture format, so a zyxh order in a bgra texture
							  should still be passed as 'xyzh'. Capitals are allowed.
							  Any character stored here that is not one of x,y,z,h
							  or a NULL character	will be ignored when PVRTexLib
							  reads the data,	but will be preserved. This is useful
							  if you wish to define a custom data channel for instance.
							  In these instances PVRTexLib will assume it is simply
							  colour data.
		*************************************************************************/
		void SetTextureBumpMap(float bumpScale, const std::string& bumpOrder)
		{
			if (m_hTextureHeader)
			{
				PVRTexLib_SetTextureBumpMap(m_hTextureHeader, bumpScale, bumpOrder.c_str());
			}
		}

		/*!***********************************************************************
		 @brief		Sets the texture atlas coordinate meta data for later display.
					It is up to the user to make sure that this texture atlas
					data actually makes sense in the context of the header.
		 @param[in]	atlasData Pointer to an array of atlas data.
		 @param[in]	dataSize Number of floats in atlasData.
		*************************************************************************/
		void SetTextureAtlas(const float* atlasData, PVRTuint32 dataSize)
		{
			if (m_hTextureHeader)
			{
				PVRTexLib_SetTextureAtlas(m_hTextureHeader, atlasData, dataSize);
			}
		}

		/*!***********************************************************************
		 @brief     Sets the texture's face ordering.
		 @param[in]	cubeMapOrder	Up to 6 character string, with values
									x,X,y,Y,z,Z in some combination. Not all
									values need to be present. Denotes face
									order; Capitals refer to positive axis
									positions and small letters refer to
									negative axis positions. E.g. x=X-Negative,
									X=X-Positive. It is possible to have only
									some of these values rather than all, as
									long as they are NULL terminated.
									NB: Values past the 6th character are not read.
		*************************************************************************/
		void SetTextureCubeMapOrder(const std::string& cubeMapOrder)
		{
			if (m_hTextureHeader)
			{
				PVRTexLib_SetTextureCubeMapOrder(m_hTextureHeader, cubeMapOrder.c_str());
			}
		}

		/*!***********************************************************************
		 @brief     Sets a texture's border size data. This value is subtracted
					from the current texture height/width/depth to get the valid
					texture data.
		 @param[in]	borderWidth   Border width
		 @param[in]	borderHeight  Border height
		 @param[in]	borderDepth   Border depth
		*************************************************************************/
		void SetTextureBorder(PVRTuint32 borderWidth, PVRTuint32 borderHeight, PVRTuint32 borderDepth)
		{
			if (m_hTextureHeader)
			{
				PVRTexLib_SetTextureBorder(m_hTextureHeader, borderWidth, borderHeight, borderDepth);
			}
		}

		/*!***********************************************************************
		 @brief     Adds an arbitrary piece of meta data.
		 @param[in]	dataBlock Meta data block to be added.
		*************************************************************************/
		void AddMetaData(const MetaDataBlock& dataBlock)
		{
			if (m_hTextureHeader && dataBlock.u32DataSize)
			{
				PVRTexLib_MetaDataBlock tmp;
				tmp.DevFOURCC	= dataBlock.DevFOURCC;
				tmp.u32Key		= dataBlock.u32Key;
				tmp.u32DataSize	= dataBlock.u32DataSize;
				tmp.Data		= dataBlock.Data.get();
				PVRTexLib_AddMetaData(m_hTextureHeader, &tmp);
			}
		}

		/*!***********************************************************************
		 @brief     Adds an arbitrary piece of meta data.
		 @param[in]	dataBlock Meta data block to be added.
		*************************************************************************/
		void AddMetaData(const PVRTexLib_MetaDataBlock& dataBlock)
		{
			if (m_hTextureHeader && dataBlock.u32DataSize)
			{
				PVRTexLib_AddMetaData(m_hTextureHeader, &dataBlock);
			}
		}

		/*!***********************************************************************
		 @brief     Removes a specified piece of meta data, if it exists.
		 @param[in] u32Key Key value representing the type of meta data stored.
		 @param[in]	DevFOURCC Four character descriptor representing the
							  creator of the meta data
		*************************************************************************/
		void RemoveMetaData(PVRTuint32 key, PVRTuint32 devFOURCC = PVRTEX_CURR_IDENT)
		{
			if (m_hTextureHeader)
			{
				PVRTexLib_RemoveMetaData(m_hTextureHeader, devFOURCC, key);
			}
		}

	protected:
		PVRTextureHeader(bool)
			: m_hTextureHeader()
		{
		}

		static const PVRTexLib_PVRTextureHeader GetHeader(const PVRTextureHeader& header)
		{
			return header.m_hTextureHeader;
		}

		PVRTexLib_PVRTextureHeader m_hTextureHeader;
	};

	typedef void* PVRRawTextureData;

	class PVRTexture : public PVRTextureHeader
	{
	public:
		/*!***********************************************************************
		 @brief     Creates a new texture based on a texture header,
					and optionally copies the supplied texture data.
		 @param[in]	header A PVRTextureHeader.
		 @param[in]	data Texture data (may be NULL)
		 @return	A new texture handle.
		*************************************************************************/
		PVRTexture(const PVRTextureHeader& header,
			const void *textureData)
			: PVRTextureHeader(false)
			, m_hTexture(PVRTexLib_CreateTexture(GetHeader(header), textureData))
		{
			m_hTextureHeader = PVRTexLib_GetTextureHeaderW(m_hTexture);
		}

		/*!***********************************************************************
		 @brief     Creates a new texture from a file.
					Accepted file formats are: PVR, KTX, KTX2, ASTC, DDS,
					PNG, JPEG, BMP, TGA, GIF, HDR, PSD, PPM, PGM and PIC
		 @param[in] filePath  File path to a texture to load from.
		 @return	A new texture.
		*************************************************************************/
		PVRTexture(const std::string& filePath)
			: PVRTextureHeader(false)
			, m_hTexture(PVRTexLib_CreateTextureFromFile(filePath.c_str()))
		{
			if (m_hTexture)
			{
				m_hTextureHeader = PVRTexLib_GetTextureHeaderW(m_hTexture);
			}
		}

		/*!***********************************************************************
		 @brief     Creates a new texture from a pointer that includes a header
					structure, meta data and texture data as laid out in a file.
					This functionality is primarily for user-defined file loading.
					Header may be any version of PVR.
		 @param[in]	data Pointer to texture data
		 @return	A new texture.
		*************************************************************************/
		explicit PVRTexture(const PVRRawTextureData data)
			: PVRTextureHeader(false)
			, m_hTexture(PVRTexLib_CreateTextureFromData(data))
		{
			if (m_hTexture)
			{
				m_hTextureHeader = PVRTexLib_GetTextureHeaderW(m_hTexture);
			}
		}

		/*!***********************************************************************
		 @brief     Creates a copy of the supplied texture.
		 @param[in]	texture A PVRTexture to copy from.
		 @return	A new texture.
		*************************************************************************/
		PVRTexture(const PVRTexture& rhs)
			: PVRTextureHeader(false)
			, m_hTexture(PVRTexLib_CopyTexture(rhs.m_hTexture))
		{
			m_hTextureHeader = PVRTexLib_GetTextureHeaderW(m_hTexture);
		}

		/*!***********************************************************************
		 @brief     Creates a new texture, moving the contents of the
					supplied texture into the new texture.
		 @param[in]	texture A PVRTexture to move from.
		 @return	A new texture.
		*************************************************************************/
		PVRTexture(PVRTexture&& rhs)
			: PVRTextureHeader(false)
			, m_hTexture(rhs.m_hTexture)
		{
			m_hTextureHeader = rhs.m_hTextureHeader;
			rhs.m_hTextureHeader = nullptr;
			rhs.m_hTexture = nullptr;
		}

		/*!***********************************************************************
		 @brief     Copies the contents of another texture into this one.
		 @param[in]	rhs Texture to copy
		 @return	This texture.
		*************************************************************************/
		PVRTexture& operator=(const PVRTexture& rhs)
		{
			if (&rhs == this)
				return *this;

			if (m_hTexture)
			{
				PVRTexLib_DestroyTexture(m_hTexture);
				m_hTexture = nullptr;
				m_hTextureHeader = nullptr;
			}

			m_hTexture = PVRTexLib_CopyTexture(rhs.m_hTexture);
			m_hTextureHeader = PVRTexLib_GetTextureHeaderW(m_hTexture);

			return *this;
		}

		/*!***********************************************************************
		 @brief     Moves ownership of texture data to this object.
		 @param[in]	rhs Texture to move
		 @return	This texture.
		*************************************************************************/
		PVRTexture& operator=(PVRTexture&& rhs)
		{
			if (&rhs == this)
				return *this;

			if (m_hTexture)
			{
				PVRTexLib_DestroyTexture(m_hTexture);
				m_hTexture = nullptr;
				m_hTextureHeader = nullptr;
			}

			m_hTexture = rhs.m_hTexture;
			m_hTextureHeader = rhs.m_hTextureHeader;

			rhs.m_hTextureHeader = nullptr;
			rhs.m_hTexture = nullptr;
			return *this;
		}

		~PVRTexture()
		{
			if (m_hTexture)
			{
				PVRTexLib_DestroyTexture(m_hTexture);
				m_hTexture = nullptr;
				m_hTextureHeader = nullptr;
			}
		}

		/*!***********************************************************************
		 @brief     Returns a pointer into the texture's data.
					The data offset is calculated using the parameters below.
		 @param[in]	MIPLevel Offset to MIP Map levels
		 @param[in]	arrayMember Offset to array members
		 @param[in]	faceNumber Offset to face numbers
		 @param[in]	ZSlice Offset to Z slice (3D textures only)
		 @return	Pointer into the texture data OR NULL on failure.
		*************************************************************************/
		void* GetTextureDataPointer(
			PVRTuint32 MIPLevel = 0U,
			PVRTuint32 arrayMember = 0U,
			PVRTuint32 faceNumber = 0U,
			PVRTuint32 ZSlice = 0U) const
		{
			if (m_hTexture)
			{
				return PVRTexLib_GetTextureDataPtr(m_hTexture, MIPLevel, arrayMember, faceNumber, ZSlice);
			}
			else
			{
				return nullptr;
			}
		}

		/*!***********************************************************************
		 @brief 	Pads the texture data to a boundary value equal to "padding".
					For example setting padding=8 will align the start of the
					texture data to an 8 byte boundary.
					NB: This should be called immediately before saving as
					the value is worked out based on the current meta data size.
		 @param[in]	padding Padding boundary value
		*************************************************************************/
		void AddPaddingMetaData(PVRTuint32 padding)
		{
			if (m_hTexture)
			{
				PVRTexLib_AddPaddingMetaData(m_hTexture, padding);
			}
		}

		/*!***********************************************************************
		 @brief     Saves the texture to a given file path.
					File type will be determined by the extension present in the string.
					Valid extensions are: PVR, KTX, KTX2, ASTC, DDS and h
					If no extension is present the PVR format will be selected.
					Unsupported formats will result in failure.
		 @param[in]	filepath File path to write to
		 @return	True if the method succeeds.
		*************************************************************************/
		bool SaveToFile(const std::string& filePath) const
		{
			if (m_hTexture)
			{
				return PVRTexLib_SaveTextureToFile(m_hTexture, filePath.c_str());
			}
			else
			{
				return false;
			}
		}

		/*!***********************************************************************
		 @brief     Saves the texture to a file, stripping any
					extensions specified and appending .pvr. This function is
					for legacy support only and saves out to PVR Version 2 file.
					The target api must be specified in order to save to this format.
		 @param[in]	filepath File path to write to
		 @param[in]	api Target API
		 @return	True if the method succeeds.
		*************************************************************************/
		bool SaveToFile(const std::string& filePath, PVRTexLibLegacyApi api) const
		{
			if (m_hTexture)
			{
				return PVRTexLib_SaveTextureToLegacyPVRFile(m_hTexture, filePath.c_str(), api);
			}
			else
			{
				return false;
			}
		}

		/*!***********************************************************************
		@brief		Writes out a single surface to a given image file.
		@details	File type is determined by the extension present in the filepath string.
					Supported file types are PNG, JPG, BMP, TGA and HDR.
					If no extension is present then the PNG format will be selected.
					Unsupported formats will result in failure.
		@param[in]	filepath Path to write the image file.
		@param[in]	MIPLevel Mip level.
		@param[in]	arrayMember Array index.
		@param[in]	face Face index.
		@param[in]	ZSlice Z index.
		@return		True if the method succeeds.
		*************************************************************************/
		bool SaveSurfaceToImageFile(const std::string& filePath, PVRTuint32 MIPLevel = 0U, PVRTuint32 arrayMember = 0U, PVRTuint32 face = 0U, PVRTuint32 ZSlice = 0U) const
		{
			if (m_hTexture)
			{
				return PVRTexLib_SaveSurfaceToImageFile(m_hTexture, filePath.c_str(), MIPLevel, arrayMember, face, ZSlice);
			}
			else
			{
				return false;
			}
		}

		/*!***********************************************************************
		 @brief     Resizes the texture to new specified dimensions.
		 @param[in]	newWidth New width
		 @param[in]	newHeight New height
		 @param[in]	newDepth New depth
		 @param[in]	resizeMode Filtering mode
		 @return	True if the method succeeds.
		*************************************************************************/
		bool Resize(
			PVRTuint32 newWidth,
			PVRTuint32 newHeight,
			PVRTuint32 newDepth,
			PVRTexLibResizeMode resizeMode)
		{
			if (m_hTexture)
			{
				return PVRTexLib_ResizeTexture(m_hTexture, newWidth, newHeight, newDepth, resizeMode);
			}
			else
			{
				return false;
			}
		}

		/*!***********************************************************************
		 @brief		Resizes the canvas of a texture to new specified dimensions.
		 			Offset area is filled with transparent black colour.
		 @param[in]	u32NewWidth		New width
		 @param[in]	u32NewHeight	New height
		 @param[in]	u32NewDepth     New depth
		 @param[in]	i32XOffset      X Offset value from the top left corner
		 @param[in]	i32YOffset      Y Offset value from the top left corner
		 @param[in]	i32ZOffset      Z Offset value from the top left corner
		 @return	True if the method succeeds.
		*************************************************************************/
		bool ResizeCanvas(
			PVRTuint32 newWidth,
			PVRTuint32 newHeight,
			PVRTuint32 newDepth,
			PVRTint32 xOffset,
			PVRTint32 yOffset,
			PVRTint32 zOffset)
		{
			if (m_hTexture)
			{
				return PVRTexLib_ResizeTextureCanvas(m_hTexture, newWidth, newHeight, newDepth, xOffset, yOffset, zOffset);
			}
			else
			{
				return false;
			}
		}

		/*!***********************************************************************
		 @brief     Rotates a texture by 90 degrees around the given axis.
		 @param[in]	rotationAxis   Rotation axis
		 @param[in]	forward        Direction of rotation; true = clockwise, false = anti-clockwise
		 @return	True if the method succeeds or not.
		*************************************************************************/
		bool Rotate(PVRTexLibAxis rotationAxis, bool forward)
		{
			if (m_hTexture)
			{
				return PVRTexLib_RotateTexture(m_hTexture, rotationAxis, forward);
			}
			else
			{
				return false;
			}
		}

		/*!***********************************************************************
		 @brief     Flips a texture on a given axis.
		 @param[in]	flipDirection  Flip direction
		 @return	True if the method succeeds.
		*************************************************************************/
		bool Flip(PVRTexLibAxis flipDirection)
		{
			if (m_hTexture)
			{
				return PVRTexLib_FlipTexture(m_hTexture, flipDirection);
			}
			else
			{
				return false;
			}
		}

		/*!***********************************************************************
		 @brief     Adds a user specified border to the texture.
		 @param[in]	borderX	X border
		 @param[in]	borderY	Y border
		 @param[in]	borderZ	Z border
		 @return	True if the method succeeds.
		*************************************************************************/
		bool Border(PVRTuint32 borderX, PVRTuint32 borderY, PVRTuint32 borderZ)
		{
			if (m_hTexture)
			{
				return PVRTexLib_BorderTexture(m_hTexture, borderX, borderY, borderZ);
			}
			else
			{
				return false;
			}
		}

		/*!***********************************************************************
		 @brief     Pre-multiplies a texture's colours by its alpha values.
		 @return	True if the method succeeds.
		*************************************************************************/
		bool PreMultiplyAlpha()
		{
			if (m_hTexture)
			{
				return PVRTexLib_PreMultiplyAlpha(m_hTexture);
			}
			else
			{
				return false;
			}
		}

		/*!***********************************************************************
		 @brief     Allows a texture's colours to run into any fully transparent areas.
		 @return	True if the method succeeds.
		*************************************************************************/
		bool Bleed()
		{
			if (m_hTexture)
			{
				return PVRTexLib_Bleed(m_hTexture);
			}
			else
			{
				return false;
			}
		}

		/*!***********************************************************************
		 @brief     Sets the specified number of channels to values specified in pValues.
		 @param[in]	numChannelSets	Number of channels to set
		 @param[in]	channels		Channels to set
		 @param[in]	pValues			uint32 values to set channels to
		 @return	True if the method succeeds.
		*************************************************************************/
		bool SetChannels(
			PVRTuint32 numChannelSets,
			const PVRTexLibChannelName* channels,
			const PVRTuint32* pValues)
		{
			if (m_hTexture)
			{
				return PVRTexLib_SetTextureChannels(m_hTexture, numChannelSets, channels, pValues);
			}
			else
			{
				return false;
			}
		}

		/*!***********************************************************************
		 @brief     Sets the specified number of channels to values specified in float pValues.
		 @param[in]	numChannelSets	Number of channels to set
		 @param[in]	channels		Channels to set
		 @param[in]	pValues			float values to set channels to
		 @return	True if the method succeeds.
		*************************************************************************/
		bool SetChannels(
			PVRTuint32 numChannelSets,
			const PVRTexLibChannelName* channels,
			const float* pValues)
		{
			if (m_hTexture)
			{
				return PVRTexLib_SetTextureChannelsFloat(m_hTexture, numChannelSets, channels, pValues);
			}
			else
			{
				return false;
			}
		}

		/*!***********************************************************************
		 @brief     Copies the specified channels from textureSource
		 			into textureDestination. textureSource is not modified so it
		 			is possible to use the same texture as both input and output.
		 			When using the same texture as source and destination, channels
		 			are preserved between swaps e.g. copying Red to Green and then
		 			Green to Red will result in the two channels trading places
		 			correctly. Channels in eChannels are set to the value of the channels
		 			in eChannelSource.
		 @param[in]	sourceTexture		A PVRTexture to copy channels from.
		 @param[in]	uiNumChannelCopies	Number of channels to copy
		 @param[in]	destinationChannels	Channels to set
		 @param[in]	sourceChannels		Source channels to copy from
		 @return	True if the method succeeds.
		*************************************************************************/
		bool CopyChannels(
			const PVRTexture& sourceTexture,
			PVRTuint32 numChannelCopies,
			const PVRTexLibChannelName* destinationChannels,
			const PVRTexLibChannelName* sourceChannels)
		{
			if (m_hTexture)
			{
				return PVRTexLib_CopyTextureChannels(m_hTexture, sourceTexture.m_hTexture, numChannelCopies, destinationChannels, sourceChannels);
			}
			else
			{
				return false;
			}
		}

		/*!***********************************************************************
		 @brief     Generates a Normal Map from a given height map.
					Assumes the red channel has the height values.
					By default outputs to red/green/blue = x/y/z,
					this can be overridden by specifying a channel
					order in channelOrder. The channels specified
					will output to red/green/blue/alpha in that order.
					So "xyzh" maps x to red, y to green, z to blue
					and h to alpha. 'h' is used to specify that the
					original height map data should be preserved in
					the given channel.
		 @param[in]	fScale			Scale factor
		 @param[in]	channelOrder	Channel order
		 @return	True if the method succeeds.
		*************************************************************************/
		bool GenerateNormalMap(float fScale, const std::string& channelOrder)
		{
			if (m_hTexture)
			{
				return PVRTexLib_GenerateNormalMap(m_hTexture, fScale, channelOrder.c_str());
			}
			else
			{
				return false;
			}
		}

		/*!***********************************************************************
		 @brief     Generates MIPMap chain for a texture.
		 @param[in]	filterMode	Filter mode
		 @param[in]	mipMapsToDo	Number of levels of MIPMap chain to create.
								Use PVRTEX_ALLMIPLEVELS to create a full mip chain.
		 @return	True if the method succeeds.
		*************************************************************************/
		bool GenerateMIPMaps(PVRTexLibResizeMode filterMode, PVRTint32 mipMapsToDo = PVRTEX_ALLMIPLEVELS)
		{
			if (m_hTexture)
			{
				return PVRTexLib_GenerateMIPMaps(m_hTexture, filterMode, mipMapsToDo);
			}
			else
			{
				return false;
			}
		}

		/*!***********************************************************************
		 @brief     Colours a texture's MIPMap levels with different colours
					for debugging purposes. MIP levels are coloured in the
					following repeating pattern: Red, Green, Blue, Cyan,
					Magenta and Yellow
		 @return	True if the method succeeds.
		*************************************************************************/
		bool ColourMIPMaps()
		{
			if (m_hTexture)
			{
				return PVRTexLib_ColourMIPMaps(m_hTexture);
			}
			else
			{
				return false;
			}
		}

		/*!***********************************************************************
		 @brief     Transcodes a texture from its original format into the specified format.
					Will either quantise or dither to lower precisions based on "bDoDither".
					"quality" specifies the quality for compressed formats:	PVRTC, ETC,
					ASTC, and BASISU. Higher quality generally means a longer computation time.
		 @param[in]	pixelFormat	Pixel format type
		 @param[in]	channelType	Channel type
		 @param[in]	colourspace	Colour space
		 @param[in]	quality		Quality level for compresssed formats,
								higher quality usually requires more processing time
		 @param[in]	doDither	Dither the texture to lower precisions
		 @return	True if the method succeeds.
		*************************************************************************/
		bool Transcode(
			PVRTuint64 pixelFormat,
			PVRTexLibVariableType channelType,
			PVRTexLibColourSpace colourspace,
			PVRTexLibCompressorQuality quality = PVRTexLibCompressorQuality::PVRTLCQ_PVRTCNormal,
			bool doDither = false,
			float maxRange = 1.0f)
		{
			if (m_hTexture)
			{
				
				PVRTexLib_TranscoderOptions options;
				options.sizeofStruct = sizeof(PVRTexLib_TranscoderOptions);
				options.pixelFormat = pixelFormat;
				options.channelType[0] = options.channelType[1] = options.channelType[2] = options.channelType[3] = channelType;
				options.colourspace = colourspace;
				options.quality = quality;
				options.doDither = doDither;
				options.maxRange = maxRange;
				return PVRTexLib_TranscodeTexture(m_hTexture, options);
			}
			else
			{
				return false;
			}
		}

		/*!***********************************************************************
		 @brief     Transcodes a texture from its original format into the specified format.
		 @param[in]	options	struct containing transcoder options.
		 @return	True if the method succeeds.
		*************************************************************************/
		bool Transcode(const PVRTexLib_TranscoderOptions& options)
		{
			if (m_hTexture)
			{
				return PVRTexLib_TranscodeTexture(m_hTexture, options);
			}
			else
			{
				return false;
			}
		}

		/*!***********************************************************************
		 @brief     Creates a cubemap with six faces from an equirectangular
					projected texture. The input must have an aspect ratio of 2:1,
					i.e. the width must be exactly twice the height.
		 @param[in]	filterMode Filtering mode to apply when sampling the source texture.
		 @return	True if the method succeeds.
		*************************************************************************/
		bool EquiRectToCubeMap(PVRTexLibResizeMode filter)
		{
			if (m_hTexture)
			{
				return PVRTexLib_EquiRectToCubeMap(m_hTexture, filter);
			}
			else
			{
				return false;
			}
		}

	protected:
		PVRTexLib_PVRTexture m_hTexture;
	};
}
/*****************************************************************************
End of file (PVRTexLib.hpp)
*****************************************************************************/