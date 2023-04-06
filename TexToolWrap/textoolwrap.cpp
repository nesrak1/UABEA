//for crunch lol
#if defined(_WIN32)
#define WIN32
#endif

#include "PVRTexLib/Include/PVRTexLib.hpp"
#include "ispc/include/ispc_texcomp.h"
#include "crunch/inc/crnlib.h"
#include "crunch/inc/crn_decomp.h"
#include <cstring>
#include <stdio.h>
#include <map>

#if defined(_MSC_VER)
	#define EXPORT extern "C" __declspec(dllexport)
#elif defined(__GNUC__)
	#define EXPORT extern "C" __attribute__((visibility("default")))
#endif

std::map<int, void*> memoryPickup;
int nextMemoryPickupId = 0;

bool GetPVRTexLibModes(int mode, PVRTuint64& pvrtlMode, PVRTexLibVariableType& pvrtlVarType) {
	switch (mode) {
		case 5:  pvrtlMode = PVRTGENPIXELID4('a','r','g','b', 8, 8, 8, 8); break; //ARGB32
		case 14: pvrtlMode = PVRTGENPIXELID4('b','g','r','a', 8, 8, 8, 8); break; //BGRA32
		case 4:  pvrtlMode = PVRTGENPIXELID4('r','g','b','a', 8, 8, 8, 8); break; //RGBA32
		case 3:  pvrtlMode = PVRTGENPIXELID4('r','g','b', 0 , 8, 8, 8, 0); break; //RGB24
		case 2:  pvrtlMode = PVRTGENPIXELID4('a','r','g','b', 4, 4, 4, 4); break; //ARGB4444
		case 13: pvrtlMode = PVRTGENPIXELID4('r','g','b','a', 4, 4, 4, 4); break; //RGBA4444
		case 7:  pvrtlMode = PVRTGENPIXELID4('r','g','b', 0 , 5, 6, 5, 0); break; //RGB565
		case 1:  pvrtlMode = PVRTGENPIXELID4('a', 0 , 0 , 0 , 8, 0, 0, 0); break; //Alpha8
		case 63: pvrtlMode = PVRTGENPIXELID4('r', 0 , 0 , 0 , 8, 0, 0, 0); break; //R8
		case 9:  pvrtlMode = PVRTGENPIXELID4('r', 0 , 0 , 0 ,16, 0, 0, 0); break; //R16
		case 62: pvrtlMode = PVRTGENPIXELID4('r','g', 0 , 0 ,16,16, 0, 0); break; //RG16
		case 15: pvrtlMode = PVRTGENPIXELID4('r', 0 , 0 , 0 ,16, 0, 0, 0); break; //RHalf
		case 16: pvrtlMode = PVRTGENPIXELID4('r','g', 0 , 0 ,16,16, 0, 0); break; //RGHalf
		case 17: pvrtlMode = PVRTGENPIXELID4('r','g','b','a',16,16,16,16); break; //RGBAHalf
		case 18: pvrtlMode = PVRTGENPIXELID4('r', 0 , 0 , 0 ,32, 0, 0, 0); break; //RFloat
		case 19: pvrtlMode = PVRTGENPIXELID4('r','g', 0 , 0 ,32,32, 0, 0); break; //RGFloat
		case 20: pvrtlMode = PVRTGENPIXELID4('r','g','b','a',32,32,32,32); break; //RGBAFloat
		case 41: pvrtlMode = PVRTLPF_EAC_R11; break;
		case 42: pvrtlMode = PVRTLPF_EAC_R11; break; //idk
		case 43: pvrtlMode = PVRTLPF_EAC_RG11; break;
		case 44: pvrtlMode = PVRTLPF_EAC_RG11; break; //idk
		case 34: pvrtlMode = PVRTLPF_ETC1; break;
		//case 60: break; //idk
		//case 61: break; //idk
		case 45: pvrtlMode = PVRTLPF_ETC2_RGB; break;
		case 46: pvrtlMode = PVRTLPF_ETC2_RGB_A1; break;
		case 47: pvrtlMode = PVRTLPF_ETC2_RGBA; break;
		case 30: pvrtlMode = PVRTLPF_PVRTCI_2bpp_RGB; break;
		case 31: pvrtlMode = PVRTLPF_PVRTCI_2bpp_RGBA; break;
		case 32: pvrtlMode = PVRTLPF_PVRTCI_4bpp_RGB; break;
		case 33: pvrtlMode = PVRTLPF_PVRTCI_4bpp_RGBA; break;
		case 48: pvrtlMode = PVRTLPF_ASTC_4x4; break;
		case 49: pvrtlMode = PVRTLPF_ASTC_5x5; break;
		case 50: pvrtlMode = PVRTLPF_ASTC_6x6; break;
		case 51: pvrtlMode = PVRTLPF_ASTC_8x8; break;
		case 52: pvrtlMode = PVRTLPF_ASTC_10x10; break;
		case 53: pvrtlMode = PVRTLPF_ASTC_12x12; break;
		case 54: pvrtlMode = PVRTLPF_ASTC_4x4; break; //idk
		case 55: pvrtlMode = PVRTLPF_ASTC_5x5; break; //idk
		case 56: pvrtlMode = PVRTLPF_ASTC_6x6; break; //idk
		case 57: pvrtlMode = PVRTLPF_ASTC_8x8; break; //idk
		case 58: pvrtlMode = PVRTLPF_ASTC_10x10; break; //idk
		case 59: pvrtlMode = PVRTLPF_ASTC_12x12; break; //idk
		default: return false; //idk
	}
	
	switch (mode) {
		case 15: pvrtlVarType = PVRTLVT_SignedFloat; break; //RHalf
		case 16: pvrtlVarType = PVRTLVT_SignedFloat; break; //RGHalf
		case 17: pvrtlVarType = PVRTLVT_SignedFloat; break; //RGBAHalf
		case 18: pvrtlVarType = PVRTLVT_SignedFloat; break; //RFloat
		case 19: pvrtlVarType = PVRTLVT_SignedFloat; break; //RGFloat
		case 20: pvrtlVarType = PVRTLVT_SignedFloat; break; //RGBAFloat
		default: pvrtlVarType = PVRTLVT_UnsignedByteNorm; break;
	}
	
	return true;
}

PVRTexLibCompressorQuality GetPVRTexLibCompressionLevel(PVRTuint64 pvrtlMode) {
	switch (pvrtlMode) {
		case PVRTLPF_PVRTCI_2bpp_RGB:
		case PVRTLPF_PVRTCI_2bpp_RGBA:
		case PVRTLPF_PVRTCI_4bpp_RGB:
		case PVRTLPF_PVRTCI_4bpp_RGBA:
		case PVRTLPF_PVRTCII_2bpp:
		case PVRTLPF_PVRTCII_4bpp:
			return PVRTLCQ_PVRTCNormal;
		case PVRTLPF_ETC1:
		case PVRTLPF_ETC2_RGB:
		case PVRTLPF_ETC2_RGBA:
		case PVRTLPF_ETC2_RGB_A1:
		case PVRTLPF_EAC_R11:
		case PVRTLPF_EAC_RG11:
			return PVRTLCQ_ETCNormal;
		case PVRTLPF_ASTC_4x4:
		case PVRTLPF_ASTC_5x4:
		case PVRTLPF_ASTC_5x5:
		case PVRTLPF_ASTC_6x5:
		case PVRTLPF_ASTC_6x6:
		case PVRTLPF_ASTC_8x5:
		case PVRTLPF_ASTC_8x6:
		case PVRTLPF_ASTC_8x8:
		case PVRTLPF_ASTC_10x5:
		case PVRTLPF_ASTC_10x6:
		case PVRTLPF_ASTC_10x8:
		case PVRTLPF_ASTC_10x10:
		case PVRTLPF_ASTC_12x10:
		case PVRTLPF_ASTC_12x12:
		case PVRTLPF_ASTC_3x3x3:
		case PVRTLPF_ASTC_4x3x3:
		case PVRTLPF_ASTC_4x4x3:
		case PVRTLPF_ASTC_4x4x4:
		case PVRTLPF_ASTC_5x4x4:
		case PVRTLPF_ASTC_5x5x4:
		case PVRTLPF_ASTC_5x5x5:
		case PVRTLPF_ASTC_6x5x5:
		case PVRTLPF_ASTC_6x6x5:
		case PVRTLPF_ASTC_6x6x6:
			return PVRTLCQ_ASTCMedium;
		default:
			return PVRTLCQ_PVRTCNormal;
	}
}

EXPORT unsigned int DecodeByPVRTexLib(void* data, void* outBuf, int mode, unsigned int width, unsigned int height) {
	PVRTuint64 pvrtlMode;
	PVRTexLibVariableType pvrtlVarType;
	
	if (!GetPVRTexLibModes(mode, pvrtlMode, pvrtlVarType)) {
		return 0;
	}
	
	unsigned long long RGBA8888 = PVRTGENPIXELID4('r','g','b','a', 8,8,8,8);
	pvrtexlib::PVRTextureHeader pvrth = pvrtexlib::PVRTextureHeader(pvrtlMode, width, height, 1,1,1,1, PVRTLCS_sRGB, pvrtlVarType);
	pvrtexlib::PVRTexture pvrt = pvrtexlib::PVRTexture(pvrth, data);
	
	if (!pvrt.Transcode(RGBA8888, PVRTLVT_UnsignedByteNorm, PVRTLCS_sRGB, PVRTLCQ_PVRTCNormal, false)) {
		return 0;
	}
	
	void* newData = pvrt.GetTextureDataPointer();
	unsigned int size = pvrt.GetTextureDataSize();
	memcpy(outBuf, newData, size);
	return size;
}
EXPORT unsigned int EncodeByPVRTexLib(void* data, void* outBuf, int mode, int level, unsigned int width, unsigned int height) {
	PVRTuint64 pvrtlMode;
	PVRTexLibVariableType pvrtlVarType;
	
	if (!GetPVRTexLibModes(mode, pvrtlMode, pvrtlVarType)) {
		return 0;
	}
	PVRTexLibCompressorQuality compLevel = GetPVRTexLibCompressionLevel(pvrtlMode);
	
	unsigned long long RGBA8888 = PVRTGENPIXELID4('r','g','b','a', 8,8,8,8);
	pvrtexlib::PVRTextureHeader pvrth = pvrtexlib::PVRTextureHeader(RGBA8888, width, height);
	pvrtexlib::PVRTexture pvrt = pvrtexlib::PVRTexture(pvrth, data);
	
	if (!pvrt.Transcode(pvrtlMode, pvrtlVarType, PVRTLCS_sRGB, compLevel, false)) {
		return 0;
	}
	
	void* newData = pvrt.GetTextureDataPointer();
	unsigned int size = pvrt.GetTextureDataSize();
	memcpy(outBuf, newData, size);
	return size;
}

EXPORT unsigned int EncodeByISPC(void* data, void* outBuf, int mode, int level, unsigned int width, unsigned int height) {
	rgba_surface surface;
	surface.ptr = (uint8_t*)data;
	surface.width = width;
	surface.height = height;
	surface.stride = width * 4;

	int blockCountX = (width + 3) >> 2;
	int blockCountY = (height + 3) >> 2;
	int blockByteSize = 0;

	if (mode == 10) { //DXT1
		CompressBlocksBC1(&surface, (uint8_t*)outBuf);
		blockByteSize = 8;
	} else if (mode == 12) { //DXT5
		CompressBlocksBC3(&surface, (uint8_t*)outBuf);
		blockByteSize = 16;
	}
	else if (mode == 26) { // BC4
		CompressBlocksBC4(&surface, (uint8_t*)outBuf);
		blockByteSize = 8;
	}
	else if (mode == 27) { // BC5
		CompressBlocksBC5(&surface, (uint8_t*)outBuf);
		blockByteSize = 16;
	} else if (mode == 24) { // BC6H
		bc6h_enc_settings bc6hsettings;
		GetProfile_bc6h_basic(&bc6hsettings);
		CompressBlocksBC6H(&surface, (uint8_t*)outBuf, &bc6hsettings);
		blockByteSize = 16;
	} else if (mode == 25) { //BC7
		bc7_enc_settings bc7settings;
		GetProfile_alpha_basic(&bc7settings); //GetProfile_alpha_slow
		CompressBlocksBC7(&surface, (uint8_t*)outBuf, &bc7settings);
		blockByteSize = 16;
	} else {
		return 0;
	}

	return blockCountX * blockCountY * blockByteSize;
}

EXPORT unsigned int DecodeByCrunchUnity(void* data, void* outBuf, int mode, unsigned int width, unsigned int height, unsigned int byteSize) {
	crnd::crn_texture_info tex_info;
	tex_info.m_struct_size = sizeof(crnd::crn_texture_info);
	if (!crnd_get_texture_info(data, byteSize, &tex_info)) {
		return 0;
	}

	crnd::crnd_unpack_context pContext = crnd::crnd_unpack_begin(data, byteSize);
	if (!pContext) {
		return 0;
	}

	const crnd::uint level_width = crnd::math::maximum<crnd::uint>(1U, tex_info.m_width);
	const crnd::uint level_height = crnd::math::maximum<crnd::uint>(1U, tex_info.m_height);
	const crnd::uint num_blocks_x = (level_width + 3U) >> 2U;
	const crnd::uint num_blocks_y = (level_height + 3U) >> 2U;

	const crnd::uint row_pitch = num_blocks_x * tex_info.m_bytes_per_block;
	const crnd::uint size_of_face = num_blocks_y * row_pitch;

	bool success = crnd::crnd_unpack_level(pContext, &outBuf, size_of_face, row_pitch, 0);
	crnd::crnd_unpack_end(pContext);

	if (success) {
		return size_of_face;
	} else {
		return 0;
	}
}

// todo: we need to use two different versions of crunch: the original and the unity fork.
// currently we just use the unity fork. need to look into when and where to use the original one.
EXPORT unsigned int EncodeByCrunchUnity(void* data, int* checkoutId, int mode, int level, unsigned int width, unsigned int height, unsigned int ver, int mips) {
	crn_comp_params comp_params;
	comp_params.m_width = width;
	comp_params.m_height = height;
	comp_params.set_flag(cCRNCompFlagPerceptual, true);
	//comp_params.set_flag(cCRNCompFlagDXT1AForTransparency, false); //unsure if unity dxt1 is ever transparent?
	comp_params.set_flag(cCRNCompFlagHierarchical, true);
	comp_params.m_file_type = cCRNFileTypeCRN;

	switch (mode) {
		case 28:
			comp_params.m_format = cCRNFmtDXT1;
			break;
		case 29:
			comp_params.m_format = cCRNFmtDXT5;
			break;
		case 64:
			comp_params.m_format = cCRNFmtETC1;
			break;
		case 65:
			comp_params.m_format = cCRNFmtETC2A;
			break;
		default:
			return 0;
	}

	comp_params.m_pImages[0][0] = (crn_uint32*)data;
	comp_params.m_quality_level = 128; //cDefaultCRNQualityLevel

	comp_params.m_userdata0 = ver; //custom version field??? idek
	comp_params.m_num_helper_threads = 1; //staying safe for xplat for now

	crn_mipmap_params mip_params;
	mip_params.m_gamma_filtering = true;

	// probably causes mass chaos if we go over since
	// the asset field wouldn't've been set but w/e
	// hope that doesn't happen here :shrugs:
	if (mips > cCRNMaxLevels) {
		mips = cCRNMaxLevels;
	} else if (mips < 0) {
		mips = 1;
	}
	if (mips == 1) {
		mip_params.m_mode = cCRNMipModeNoMips;
	} else {
		mip_params.m_mode = cCRNMipModeGenerateMips;
	}

	mip_params.m_max_levels = mips;

	crn_uint32 actual_quality_level;
	float actual_bitrate;
	crn_uint32 output_file_size;

	void* newData = crn_compress(comp_params, mip_params, output_file_size, &actual_quality_level, &actual_bitrate);

	if (checkoutId != NULL) {
		void* outBuf = malloc(output_file_size);
		if (outBuf == NULL) {
			return 0;
		}

		memcpy(outBuf, newData, output_file_size);
		
		// todo: not thread safe (although we don't do any threading right now)
		*checkoutId = nextMemoryPickupId;
		memoryPickup[nextMemoryPickupId] = outBuf;
		nextMemoryPickupId++;

		return output_file_size;
	} else {
		return 0;
	}
}

EXPORT bool PickUpAndFree(void* outBuf, unsigned int size, int id)
{
	if (memoryPickup.find(id) != memoryPickup.end()) {
		void* memory = memoryPickup[id];
		memcpy(outBuf, memory, size);
		memoryPickup.erase(id);
		free(memory);
		return true;
	}
	return false;
}