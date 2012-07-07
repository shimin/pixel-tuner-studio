// STM32_Flasher.cpp : Defines the entry point for the console application.
#include "stdafx.h"
#include <string.h>
#include <stdio.h>
#include <iostream>
#include <conio.h>
#include <io.h>         //для работы с файлами
#include <io.h>         //для работы с файлами
#include <fcntl.h>      //для работы с файлами
#include <sys\stat.h>   //для работы с файлами
#include <windows.h>

#define Buff_Size  60
#define Flash_Size_MAX 0x1C800 //Байт

#define FlashFileName "Flash.bin"
#define ComPort "COM3"
#define ComBaud 19200

#define WHO_IS_IT           ('S')
#define MK_STAT                ('i')
#define VERSION_BOOT        ('V')
#define VERSION_FW          ('v')
#define WRITE_IFLASH_MODE   ('F')
#define WRITE_EFLASH_MODE   ('E')
#define WRITE_BLOCK         ('B')
#define END_WRITE_FLASH     ('C')

/*===========================================================================
 ============================================================================
|                                  CRC                                      |
 ============================================================================
 ============================================================================*/
static const unsigned char auchCRCHi_exp[] = {
   0x00, 0xc1, 0x81, 0x40, 0x01, 0xc0, 0x80, 0x41, 0x01, 0xc0, 0x80, 0x41, 0x00, 0xc1, 0x81,
   0x40, 0x01, 0xc0, 0x80, 0x41, 0x00, 0xc1, 0x81, 0x40, 0x00, 0xc1, 0x81, 0x40, 0x01, 0xc0,
   0x80, 0x41, 0x01, 0xc0, 0x80, 0x41, 0x00, 0xc1, 0x81, 0x40, 0x00, 0xc1, 0x81, 0x40, 0x01,
   0xc0, 0x80, 0x41, 0x00, 0xc1, 0x81, 0x40, 0x01, 0xc0, 0x80, 0x41, 0x01, 0xc0, 0x80, 0x41,
   0x00, 0xc1, 0x81, 0x40, 0x01, 0xc0, 0x80, 0x41, 0x00, 0xc1, 0x81, 0x40, 0x00, 0xc1, 0x81,
   0x40, 0x01, 0xc0, 0x80, 0x41, 0x00, 0xc1, 0x81, 0x40, 0x01, 0xc0, 0x80, 0x41, 0x01, 0xc0,
   0x80, 0x41, 0x00, 0xc1, 0x81, 0x40, 0x00, 0xc1, 0x81, 0x40, 0x01, 0xc0, 0x80, 0x41, 0x01,
   0xc0, 0x80, 0x41, 0x00, 0xc1, 0x81, 0x40, 0x01, 0xc0, 0x80, 0x41, 0x00, 0xc1, 0x81, 0x40,
   0x00, 0xc1, 0x81, 0x40, 0x01, 0xc0, 0x80, 0x41, 0x01, 0xc0, 0x80, 0x41, 0x00, 0xc1, 0x81,
   0x40, 0x00, 0xc1, 0x81, 0x40, 0x01, 0xc0, 0x80, 0x41, 0x00, 0xc1, 0x81, 0x40, 0x01, 0xc0,
   0x80, 0x41, 0x01, 0xc0, 0x80, 0x41, 0x00, 0xc1, 0x81, 0x40, 0x00, 0xc1, 0x81, 0x40, 0x01,
   0xc0, 0x80, 0x41, 0x01, 0xc0, 0x80, 0x41, 0x00, 0xc1, 0x81, 0x40, 0x01, 0xc0, 0x80, 0x41,
   0x00, 0xc1, 0x81, 0x40, 0x00, 0xc1, 0x81, 0x40, 0x01, 0xc0, 0x80, 0x41, 0x00, 0xc1, 0x81,
   0x40, 0x01, 0xc0, 0x80, 0x41, 0x01, 0xc0, 0x80, 0x41, 0x00, 0xc1, 0x81, 0x40, 0x01, 0xc0,
   0x80, 0x41, 0x00, 0xc1, 0x81, 0x40, 0x00, 0xc1, 0x81, 0x40, 0x01, 0xc0, 0x80, 0x41, 0x01,
   0xc0, 0x80, 0x41, 0x00, 0xc1, 0x81, 0x40, 0x00, 0xc1, 0x81, 0x40, 0x01, 0xc0, 0x80, 0x41,
   0x00, 0xc1, 0x81, 0x40, 0x01, 0xc0, 0x80, 0x41, 0x01, 0xc0, 0x80, 0x41, 0x00, 0xc1, 0x81,
   0x40
} ;

static const unsigned char auchCRCLo_exp[] = {
   0x00, 0xc0, 0xc1, 0x01, 0xc3, 0x03, 0x02, 0xc2, 0xc6, 0x06, 0x07, 0xc7, 0x05, 0xc5, 0xc4,
   0x04, 0xcc, 0x0c, 0x0d, 0xcd, 0x0f, 0xcf, 0xce, 0x0e, 0x0a, 0xca, 0xcb, 0x0b, 0xc9, 0x09,
   0x08, 0xc8, 0xd8, 0x18, 0x19, 0xd9, 0x1b, 0xdb, 0xda, 0x1a, 0x1e, 0xde, 0xdf, 0x1f, 0xdd,
   0x1d, 0x1c, 0xdc, 0x14, 0xd4, 0xd5, 0x15, 0xd7, 0x17, 0x16, 0xd6, 0xd2, 0x12, 0x13, 0xd3,
   0x11, 0xd1, 0xd0, 0x10, 0xf0, 0x30, 0x31, 0xf1, 0x33, 0xf3, 0xf2, 0x32, 0x36, 0xf6, 0xf7,
   0x37, 0xf5, 0x35, 0x34, 0xf4, 0x3c, 0xfc, 0xfd, 0x3d, 0xff, 0x3f, 0x3e, 0xfe, 0xfa, 0x3a,
   0x3b, 0xfb, 0x39, 0xf9, 0xf8, 0x38, 0x28, 0xe8, 0xe9, 0x29, 0xeb, 0x2b, 0x2a, 0xea, 0xee,
   0x2e, 0x2f, 0xef, 0x2d, 0xed, 0xec, 0x2c, 0xe4, 0x24, 0x25, 0xe5, 0x27, 0xe7, 0xe6, 0x26,
   0x22, 0xe2, 0xe3, 0x23, 0xe1, 0x21, 0x20, 0xe0, 0xa0, 0x60, 0x61, 0xa1, 0x63, 0xa3, 0xa2,
   0x62, 0x66, 0xa6, 0xa7, 0x67, 0xa5, 0x65, 0x64, 0xa4, 0x6c, 0xac, 0xad, 0x6d, 0xaf, 0x6f,
   0x6e, 0xae, 0xaa, 0x6a, 0x6b, 0xab, 0x69, 0xa9, 0xa8, 0x68, 0x78, 0xb8, 0xb9, 0x79, 0xbb,
   0x7b, 0x7a, 0xba, 0xbe, 0x7e, 0x7f, 0xbf, 0x7d, 0xbd, 0xbc, 0x7c, 0xb4, 0x74, 0x75, 0xb5,
   0x77, 0xb7, 0xb6, 0x76, 0x72, 0xb2, 0xb3, 0x73, 0xb1, 0x71, 0x70, 0xb0, 0x50, 0x90, 0x91,
   0x51, 0x93, 0x53, 0x52, 0x92, 0x96, 0x56, 0x57, 0x97, 0x55, 0x95, 0x94, 0x54, 0x9c, 0x5c,
   0x5d, 0x9d, 0x5f, 0x9f, 0x9e, 0x5e, 0x5a, 0x9a, 0x9b, 0x5b, 0x99, 0x59, 0x58, 0x98, 0x88,
   0x48, 0x49, 0x89, 0x4b, 0x8b, 0x8a, 0x4a, 0x4e, 0x8e, 0x8f, 0x4f, 0x8d, 0x4d, 0x4c, 0x8c,
   0x44, 0x84, 0x85, 0x45, 0x87, 0x47, 0x46, 0x86, 0x82, 0x42, 0x43, 0x83, 0x41, 0x81, 0x80,
   0x40
} ;


///*============================================================================*/
// Процедура расчета контрольной суммы
///*============================================================================*/
unsigned int CRC16 ( unsigned char *chkbuf, unsigned char len )
{

   unsigned char    uchCRCHi = 0xff ;
   unsigned char    uchCRCLo = 0xff ;
   unsigned int  uIndex;
   unsigned int  temp_code;
   while ( len )
      {
      uIndex = (unsigned int) ( uchCRCHi ^ *chkbuf++ ) ;

      uchCRCHi = (unsigned char) ( uchCRCLo ^ auchCRCHi_exp[uIndex] ) ;
      uchCRCLo = auchCRCLo_exp[uIndex];
      len-- ;
      }
   temp_code = (unsigned int) uchCRCHi;
   temp_code = (unsigned int) (temp_code << 8);
   temp_code |=uchCRCLo;
   return temp_code;
}

/*===========================================================================
 ============================================================================
|                        Работа с COM портом                                 |
 ============================================================================
 ============================================================================*/
static HANDLE   COMPORT = INVALID_HANDLE_VALUE;

void ComPortReset(void)
{
    DWORD   Errors;
    if (COMPORT == INVALID_HANDLE_VALUE) return;
    PurgeComm(COMPORT, PURGE_TXCLEAR | PURGE_RXCLEAR | PURGE_TXABORT | PURGE_RXABORT);
    ClearCommError(COMPORT, & Errors, 0);
}

bool ComPortClose(void)
{
    if (COMPORT == INVALID_HANDLE_VALUE) return false;
    bool   Ret =  CloseHandle(COMPORT);
    COMPORT = INVALID_HANDLE_VALUE;
    return Ret;
}

DWORD ComPortInputBuffTest(void)
{
    DWORD   Errors;
    _COMSTAT Stat;
    if (COMPORT == INVALID_HANDLE_VALUE) return 0;
    if ( !ClearCommError(COMPORT, & Errors, & Stat)) return 0;
    return Stat.cbInQue;
}

bool ComPortOpen(const char * com,  DWORD baud)
{
    DCB *           dcb;
    COMMTIMEOUTS    ct;
    int             baud_;
    

    switch (baud)
    {
    case    110 : baud_ =    CBR_110; break;
    case    300 : baud_ =    CBR_300; break;
    case    600 : baud_ =    CBR_600; break;
    case   1200 : baud_ =   CBR_1200; break;
    case   2400 : baud_ =   CBR_2400; break;
    case   4800 : baud_ =   CBR_4800; break;
    case   9600 : baud_ =   CBR_9600; break;
    case  14400 : baud_ =  CBR_14400; break;
    case  19200 : baud_ =  CBR_19200; break;
    case  38400 : baud_ =  CBR_38400; break;
    case  56000 : baud_ =  CBR_56000; break;
    case  57600 : baud_ =  CBR_57600; break;
    case 115200 : baud_ = CBR_115200; break;
    case 128000 : baud_ = CBR_128000; break;
    case 256000 : baud_ = CBR_256000; break;
    default     : return false;
    };
    
    dcb = (DCB*) HeapAlloc(GetProcessHeap(),HEAP_ZERO_MEMORY, sizeof(DC);
    if (dcb == NULL) return false;
    COMPORT  = CreateFile(com, GENERIC_READ | GENERIC_WRITE, 0, NULL, OPEN_EXISTING,NULL,NULL);
    
    if (COMPORT == INVALID_HANDLE_VALUE) goto COM_ERRORS;
    if ( ! GetCommState(COMPORT, dc )  goto COM_ERRORS;
    
    dcb->ByteSize        = 8;
    dcb->Parity          = NOPARITY;
    dcb->StopBits        = ONESTOPBIT;
    dcb->BaudRate        = baud_;
    dcb->fParity         = false;
    dcb->fOutxCtsFlow    = false;
    dcb->fOutxDsrFlow    = false;
    dcb->fDtrControl     = DTR_CONTROL_DISABLE;
    dcb->fRtsControl     = RTS_CONTROL_DISABLE;
    dcb->fDsrSensitivity = false;
    if ( ! SetCommState(COMPORT, dc )  goto COM_ERRORS;
    ct.ReadIntervalTimeout         =            200;
    ct.ReadTotalTimeoutMultiplier  =          40000 / baud_;
    ct.ReadTotalTimeoutConstant    =           200;
    ct.WriteTotalTimeoutMultiplier =             0;
    ct.WriteTotalTimeoutConstant =               0;
    if ( ! SetCommTimeouts(COMPORT, & ct) )  goto COM_ERRORS;
    
    ComPortReset();
    HeapFree(GetProcessHeap(), 0, dc; return true;

COM_ERRORS:
    HeapFree(GetProcessHeap(), 0, dc;
    ComPortClose();
    return false;
}



bool ComPortCLRDTR(void)    // Сбрасывает сигнал DTR
{
    if (COMPORT == INVALID_HANDLE_VALUE) return false;
    return  EscapeCommFunction(COMPORT, CLRDTR);
}

bool ComPortCLRRTS(void)    // Сбрасывает сигнал RTS
{
    if (COMPORT == INVALID_HANDLE_VALUE) return false;
    return  EscapeCommFunction(COMPORT, CLRRTS);
}

bool ComPortSETDTR(void)    // Устанавливет сигнал DTR
{
    if (COMPORT == INVALID_HANDLE_VALUE) return false;
    return  EscapeCommFunction(COMPORT, SETDTR);
}


bool ComPortSETRTS(void)    // Устанавливает сигнал RTS
{
    if (COMPORT == INVALID_HANDLE_VALUE) return false;
    return EscapeCommFunction(COMPORT, SETRTS);
}

bool ReadFromComPort(void * Buff, DWORD Count)
{
    if (COMPORT == INVALID_HANDLE_VALUE) return false;
    DWORD bc = 0;
    bool Ret = ReadFile(COMPORT, Buff, Count, & bc, NULL);
    if (! Ret) return false;
    if (bc != Count) Ret = false;
    return Ret;
}

bool WriteToComPort(void * Buff, DWORD Count)
{
    if (COMPORT == INVALID_HANDLE_VALUE) return false;
    DWORD bc = 0;
    if ( ! WriteFile(COMPORT, Buff, Count, & bc, NULL)) return false;
    if (bc != Count) return false;
    if ( ! FlushFileBuffers(COMPORT)) return false;
    return true;
}

 //    Вид сообщения:
 //    :       -  маркер начала сообщения
 //    1 байт  -  команда
 //    n байт  -  данные (n<250)
 //    1байт   -  маркер конца сообщения 0x0D

#define STOP_MARKER   0xD
#define MAX_DATA_SIZE 250

unsigned char ReadMess(unsigned char *Cmnd, unsigned char Data[], unsigned char *len){
  unsigned char B[255];
  unsigned char i, j;

  ReadFromComPort(B,255);
  *Cmnd=B[0];
  i;
  j=1;
  do
    {    
       Data[i++]=B[j++];
    }while ((B[j]!=STOP_MARKER)&(j<255));
    *len=i;
    return(1);
}

void WriteMess(unsigned char Cmnd, unsigned char Data[], unsigned char len){
   unsigned char B[255];
   unsigned char i,j;
   B[0] = Cmnd;
   B[1] = len;
   j=2;
   for(i;i<len;i++) B[j++]ata[i];
   B[j++]=STOP_MARKER;
   WriteToComPort(B, j);   
}




/*===========================================================================
|                 функции Чтения данных Bin файла в буфер                    |
                  Возвращает колличество считаных байт
 ============================================================================*/
unsigned int ReadBlock(FILE *file,unsigned char *Buffer){

    unsigned long Count;
    Count=ftell(file);
    fread( Buffer, Buff_Size, 1, file );
    Count=ftell(file)-Count;
    return Count;
}
/*===========================================================================
|                       Расчет контрольной суммы
 ============================================================================*/
/* adler32.c -- compute the Adler-32 checksum of a data stream
 * Copyright (C) 1995-2004 Mark Adler
 * For conditions of distribution and use, see copyright notice in zlib.h
 */

/* @(#) $Id: adler32.c,v 1.4 2007/07/05 11:47:43 andrey Exp $ */

#define BASE 65521UL    /* largest prime smaller than 65536 */
#define NMAX 5552
/* NMAX is the largest n such that 255n(n+1)/2 + (n+1)(BASE-1) <= 2^32-1 */

#define DO1(buf,i)  {adler += (buf)[i]; sum2 += adler;}
#define DO2(buf,i)  DO1(buf,i); DO1(buf,i+1);
#define DO4(buf,i)  DO2(buf,i); DO2(buf,i+2);
#define DO8(buf,i)  DO4(buf,i); DO4(buf,i+4);
#define DO16(buf)   DO8(buf,0); DO8(buf,8);

/* используем NO_DIVIDE если процессор не использует аппаратное деление */
#ifdef NO_DIVIDE
#  define MOD(a) \
    do { \
        if (a >= (BASE << 16)) a -= (BASE << 16); \
        if (a >= (BASE << 15)) a -= (BASE << 15); \
        if (a >= (BASE << 14)) a -= (BASE << 14); \
        if (a >= (BASE << 13)) a -= (BASE << 13); \
        if (a >= (BASE << 12)) a -= (BASE << 12); \
        if (a >= (BASE << 11)) a -= (BASE << 11); \
        if (a >= (BASE << 10)) a -= (BASE << 10); \
        if (a >= (BASE << 9)) a -= (BASE << 9); \
        if (a >= (BASE << 8)) a -= (BASE << 8); \
        if (a >= (BASE << 7)) a -= (BASE << 7); \
        if (a >= (BASE << 6)) a -= (BASE << 6); \
        if (a >= (BASE << 5)) a -= (BASE << 5); \
        if (a >= (BASE << 4)) a -= (BASE << 4); \
        if (a >= (BASE << 3)) a -= (BASE << 3); \
        if (a >= (BASE << 2)) a -= (BASE << 2); \
        if (a >= (BASE << 1)) a -= (BASE << 1); \
        if (a >= BASE) a -= BASE; \
    } while (0)
#  define MOD4(a) \
    do { \
        if (a >= (BASE << 4)) a -= (BASE << 4); \
        if (a >= (BASE << 3)) a -= (BASE << 3); \
        if (a >= (BASE << 2)) a -= (BASE << 2); \
        if (a >= (BASE << 1)) a -= (BASE << 1); \
        if (a >= BASE) a -= BASE; \
    } while (0)
#else
#  define MOD(a) a %= BASE
#  define MOD4(a) a %= BASE
#endif

/* ========================================================================= 
                              Фунция Adler32
 ========================================================================= */

unsigned long adler32(unsigned long adler,unsigned long *buf,unsigned long len)
{
    unsigned long sum2;
    unsigned n;

    /* split Adler-32 into component sums */
    sum2 = (adler >> 16) & 0xffff;
    adler &= 0xffff;

    /* in case user likes doing a byte at a time, keep it fast */
    if (len == 1) {
        adler += buf[0];
        if (adler >= BASE)
            adler -= BASE;
        sum2 += adler;
        if (sum2 >= BASE)
            sum2 -= BASE;
        return adler | (sum2 << 16);
    }

    /* initial Adler-32 value (deferred check for len == 1 speed) */
    if (buf == 0)
        return 1L;

    /* in case short lengths are provided, keep it somewhat fast */
    if (len < 16) {
        while (len--) {
            adler += *buf++;
            sum2 += adler;
        }
        if (adler >= BASE)
            adler -= BASE;
        MOD4(sum2);             /* only added so many BASE's */
        return adler | (sum2 << 16);
    }

    /* do length NMAX blocks -- requires just one modulo operation */
    while (len >= NMA {
        len -= NMAX;
        n = NMAX / 16;          /* NMAX is divisible by 16 */
        do {
            DO16(buf);          /* 16 sums unrolled */
            buf += 16;
        } while (--n);
        MOD(adler);
        MOD(sum2);
    }

    /* do remaining bytes (less than NMAX, still just one modulo) */
    if (len) {                  /* avoid modulos if none remaining */
        while (len >= 16) {
            len -= 16;
            DO16(buf);
            buf += 16;
        }
        while (len--) {
            adler += *buf++;
            sum2 += adler;
        }
        MOD(adler);
        MOD(sum2);
    }

    /* return recombined sums */
    return adler | (sum2 << 16);
}

//-------------------------------------------------------------------------------------------
//------------------------------------------------------------------------------------------
//                           Чтение заголовка прошивки
//-------------------------------------------------------------------------------------------
//-------------------------------------------------------------------------------------------
#define ATAG_Mag_Num          0x016f2818 /*Magic Number*/
#define ATAG_CORE             0x54410001 /* The list must start with an ATAG_CORE node */
#define ATAG_NONE             0x00000000 /* The list ends with an ATAG_NONE node. */
#define ATAG_SERIAL         0x54410006 /* board serial number. "64 bits should be enough for everybody" */
#define ATAG_REVISION         0x54410007 /* board revision */
#define ATAG_CPU             0x54410301 /* CPU

 exeland (14:28:08 3/07/2012)
*/
#define ATAG_BOARD             0x54410302 /* board */
#define ATAG_USER             0x54410303 /* user name */
#define ATAG_TIME             0x54410304 /* compile time */
#define ATAG_DATE             0x54410305 /* compile date */
#define ATAG_GCC              0x54410306 /* GCC version */

#define ATAG_SLOAD          0x54410400 /* Start Data Load Address */
#define ATAG_ELOAD           0x54410401 /* End Data Load Address */
#define ATAG_EP              0x54410402 /* Entry Point Address */
#define ATAG_DCRC            0x54410403 /* Image Data CRC Checksum */

#define ATAG_IMGADR          0x54410500 /* Where in flash the image lives */
#define ATAG_RUNLVL          0x54410501 /* A mode of image operation */
#define ATAG_LDR             0x54410502 /* Enter bootloader reason */

//My ATAG
#define ATAG_FW_VERS        0x54410601 /* FW version */
//My ATAG
#define ATAG_FW_START       0x54410602 /* FW start */
//My ATAG
#define ATAG_FW_END         0x54410603 /* FW end */


unsigned long  fread_word(FILE *file){
    unsigned char _u8[4];
    fread( _u8, 4, 1, file );
    return _u8[0]|(_u8[1]<<8)|(_u8[2]<<16)|(_u8[3]<<24);
}

/*===========================================================================
|     Ищем место расположения контрольной суммы и начало программной части
 ============================================================================*/    
bool Find_Place (FILE *file, unsigned long *DRC_Place,unsigned long *FW_Start, unsigned long *Real_FW_Size){
    
    unsigned long temp32;
    unsigned long ATAG_Type;
    unsigned long CountWordRead;
    unsigned long Place;
    bool          flg = false;

    fseek(file, 0, SEEK_SET);       // Переходим в первую позицию
    temp32 = fread_word(file);
    if (temp32==ATAG_Mag_Num){      // Первый тег
         fseek(file,4, SEEK_SET);
         temp32=fread_word(file);
         fseek(file,8, SEEK_SET);
         *Real_FW_Size=fread_word(file);
         *Real_FW_Size=*Real_FW_Size-temp32;
         fseek(file,12, SEEK_SET);  // Переходим на первый ATAG
         Place=12;
         ATAG_Type =~ATAG_NONE;
         while(ATAG_Type!=ATAG_NONE){
             CountWordRead=fread_word(file);
             ATAG_Type=fread_word(file);
             Placelace+8;        //Считали 2 слова
             CountWordRead=CountWordRead-2;
             if (ATAG_Type==ATAG_DCRC){
                 *DRC_Placelace;
                 flg = true;
             }
             Placelace+CountWordRead*4;
             fseek(file,Place,SEEK_SET);
         }
         if  (flg==false) return false;
    }
    else return false;
    *FW_Startlace;
    return true;
}





 DWORD temp, signal;    //temp - переменная-заглушка
     FILE *file;
     char* file_name = FlashFileName;
     unsigned char Buffer[Buff_Size];
     unsigned char Cmnd;
     unsigned char tempCmnd;
     unsigned char len;
     unsigned long temp32;
     unsigned long i;
     unsigned long File_Size;
     unsigned char temp8;
     unsigned long BlockLen;
     unsigned int  BlockNum;
     unsigned long DRC_Place;
     unsigned long FW_Start;
     unsigned long RealSize;
     unsigned long MyFile[Flash_Size_MAX/4];

/*===========================================================================
|                             Главная 
 ============================================================================*/
void main()
 {
    
     ;// Открываем файл для чтения
     if ((file = fopen( file_name, "r+b" )) == NULL){
         printf("File ");
         printf(FlashFileName);
         printf(" was not opened\n" );
     }
     else
     {
         printf("File ");
         printf(FlashFileName);
         printf(" was opened\n" );
         fseek(file, 0, SEEK_END);
         File_Size = ftell(file);
         printf("Firmware size: " );
         printf("%d",File_Size);
         printf("\n" );
         if (Find_Place (file,&DRC_Place, &FW_Start,&RealSize)){
             fseek(file, DRC_Place, SEEK_SET);
             temp32=fread_word(file);
             if(1){                   // Если контрольная сумма отсутствует
                 fseek(file, FW_Start, SEEK_SET);
                 temp32;
                 i=FW_Start;
                 while(i<File_Size){
                     MyFile[temp32]=fread_word(file);
                     i=ftell(file);
                     temp32++;
                 }
                 temp32=adler32(0,NULL,0);
                 temp32=adler32(temp32,&MyFile[0],RealSize/4);
                 fseek(file, DRC_Place, SEEK_SET);
                 Buffer[3]unsigned char)0x000000FF&(temp32>>24);
                 Buffer[2]unsigned char)0x000000FF&(temp32>>16);
                 Buffer[1]unsigned char)0x000000FF&(temp32>>8);
                 Buffer[0]unsigned char)0x000000FF&(temp32);
                 fwrite( &Buffer, 4, 1, file );
                 printf("DCRC LOAD k this file:");
                 printf("%d",temp32);
                 printf("\n" );
             }
             if (ComPortOpen(ComPort,ComBaud)){
               printf("Com Port was opened\n");
               Buffer[0]='0';
                  WriteMess(WHO_IS_IT, Buffer, 1);
               ReadMess(&Cmnd,Buffer,&len);
               if((Cmnd=='S')){
                 printf("Devise conect\n");
                 WriteMess(VERSION_BOOT, Buffer, 1);
                 ReadMess(&Cmnd,Buffer,&len);
                 if(Buffer[0]==1){
                    printf("Platform version 1\n");
                    WriteMess(WRITE_IFLASH_MODE, Buffer, 1);
                    Sleep(2500);
                    ReadMess(&Cmnd,Buffer,&len);
                    if(Buffer[1]=xFF){
                       printf("Flash erase!\n");  
                       BlockNum;
                       BlockLen=240;
                       fseek(file, 0, SEEK_SET); // На начало прошивки
                       while (BlockLen!){
                             BlockLen=ReadBlock(file,Buffer); //Считываем блок прошивки
                             if (BlockLen>0){
                                 printf("Send Block: " );
                                 printf("%d",BlockNum);
                                 printf(" Block Size: " );
                                 printf("%d",BlockLen);
                                 printf("\n" );
                                 printf("\n","\n");
                                 for(temp8;temp8<BlockLen;temp8++){
                                 printf("%x",Buffer[temp8]);
                                 printf(" " );
                                 }
                                WriteMess(WRITE_BLOCK, Buffer, BlockLen);
                                 Sleep(5);
                                 ReadMess(&Cmnd,Buffer,&len);
                                 if(Buffer[1]=xFF){
                                   printf("Write!\n");
                                   BlockNum++;  
                             }
                                 else{
                                     printf("Block error. \n " );
                                     break;
                                 }
                             }
                         }
                         WriteMess(END_WRITE_FLASH, Buffer, 1);
                         Sleep(100);
                         ReadMess(&Cmnd,Buffer,&len);
                         printf("Device successfully update!!.\n");

                 }
               }
               else{
                 printf("Devise not conect\n");
               }



               
             }
             else{
                     printf("Com Port was not opened\n");
                 }
             }
         }
         ComPortClose();
         fclose(file);
         while(1){
         }
     }
}