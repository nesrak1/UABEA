#include "PVRTexLib/Include/PVRTexLib.hpp"
#include <cstring>
#include <stdio.h>

#if defined(_MSC_VER)
	#define EXPORT extern "C" __declspec(dllexport)
#elif defined(__GNUC__)
	#define EXPORT extern "C" __attribute__((visibility("default")))
#endif

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
	
	unsigned long long RGBA8888 = PVRTGENPIXELID4('r','g','b','a', 8,8,8,8);
	pvrtexlib::PVRTextureHeader pvrth = pvrtexlib::PVRTextureHeader(RGBA8888, width, height);
	pvrtexlib::PVRTexture pvrt = pvrtexlib::PVRTexture(pvrth, data);
	
	if (!pvrt.Transcode(pvrtlMode, pvrtlVarType, PVRTLCS_sRGB, PVRTLCQ_PVRTCNormal, false)) {
		return 0;
	}
	
	void* newData = pvrt.GetTextureDataPointer();
	unsigned int size = pvrt.GetTextureDataSize();
	memcpy(outBuf, newData, size);
	return size;
}

EXPORT unsigned int DecodeByCrunchUnity(void* data, void* outBuf, int mode, unsigned int width, unsigned int height) {
	return 0; //todo
}
EXPORT unsigned int EncodeByCrunchUnity(void* data, void* outBuf, int mode, int level, unsigned int width, unsigned int height) {
	return 0; //todo
}