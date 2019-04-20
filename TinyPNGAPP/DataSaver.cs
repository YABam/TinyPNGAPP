using System;
using System.IO;
using System.Windows;
using Newtonsoft.Json;

namespace TinyPNGAPP
{
    public class DataSaver
    {
        static string StartPath = System.AppDomain.CurrentDomain.BaseDirectory;
        /// <summary>
		/// 判断文件是否存在
		/// </summary>
		/// <param name="fileName"></param>
		/// <returns></returns>
		private static bool IsFileExists(string fileName)
        {
            return File.Exists(fileName);
        }

        /// <summary>
        /// 判断文件夹是否存在
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        private static bool IsDirectoryExists(string fileName)
        {
            return Directory.Exists(fileName);
        }

        /// <summary>
        /// 创建一个文本文件
        /// </summary>
        /// <param name="fileName">文件路径</param>
        /// <param name="content">文件内容</param>
        private static void CreateFile(string fileName, string content)
        {
            StreamWriter streamWriter = File.CreateText(fileName);
            streamWriter.Write(content);
            streamWriter.Close();
        }

        /// <summary>
        /// 创建一个文件夹，文件夹存在则返回
        /// </summary>
        /// <param name="fileName"></param>
        private static void CreateDirectory(string fileName)
        {
            //文件夹存在则返回
            if (IsDirectoryExists(fileName))
            {
                return;
            }
            else
            {
                Directory.CreateDirectory(fileName);
            }
        }

        /// <summary>
        /// 将一个对象序列化为字符串
        /// </summary>
        /// <returns>The object.</returns>
        /// <param name="pObject">对象</param>
        private static string SerializeObject<T>(T pObject)
        {
            JsonSerializerSettings settings = new JsonSerializerSettings();
            settings.ReferenceLoopHandling = ReferenceLoopHandling.Ignore;
            //序列化后的字符串
            string serializedString = string.Empty;
            //使用Json.Net进行序列化
            serializedString = JsonConvert.SerializeObject(pObject, settings);
            return serializedString;
        }

        /// <summary>
        /// 将一个字符串反序列化为对象
        /// </summary>
        /// <returns>The object.</returns>
        /// <param name="pString">字符串</param>
        private static T DeserializeObject<T>(string pString)
        {
            //反序列化后的对象
            T deserializedObject = default(T);
            try
            {
                //使用Json.Net进行反序列化
                deserializedObject = (T)JsonConvert.DeserializeObject(pString, typeof(T));
            }
            catch
            {
                throw new Exception("文件内容不合法");
            }
            return deserializedObject;
        }

        /// <summary>
        /// 保存文档
        /// </summary>
        /// <param name="fileName">文件路径</param>
        /// <param name="pObject">要保存的对象</param>
        private static void SetData<T>(string fileName, T pObject)
        {
            //将对象序列化为字符串
            string toSave = SerializeObject(pObject);
            //对字符串进行加密,32位加密密钥
            //toSave = RijndaelEncrypt(toSave, GlobalKey);
            StreamWriter streamWriter = File.CreateText(fileName);
            streamWriter.Write(toSave);
            streamWriter.Close();
        }

        /// <summary>
        /// 解读文档
        /// </summary>
        /// <param name="fileName">文件路径</param>
        /// <returns>解读结果</returns>
        private static T GetData<T>(string fileName)
        {
            StreamReader streamReader = File.OpenText(fileName);
            string data = streamReader.ReadToEnd();
            streamReader.Close();
            return DeserializeObject<T>(data);//反序列化并返回
        }

        /// <summary>
        /// 保存数据
        /// </summary>
        /// <param name="saveIndex">存档序号</param>
        /// <param name="dataToSave">存档数据</param>
        /// <returns>是否成功</returns>
        public static bool SaveSettingData<T>(T dataToSave)
        {
            bool result = false;
            try
            {
                //定义存档路径
                //Application.path
                string dirPath = StartPath + "/Settings";
                //创建存档文件夹
                CreateDirectory(dirPath);
                //定义存档文件路径
                string fileName = dirPath + "/Config.json";

                //保存数据
                SetData(fileName, dataToSave);
                result = true;
            }
            catch (Exception ex)
            {
                //MessageBox.Show(ex.Message);
                throw ex;
                //result = false;
            }

            return result;
        }

        /// <summary>
        /// 读取保存的文件
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="loadedData"></param>
        /// <returns></returns>
        public static bool LoadSettingData<T>(out T loadedData)
        {
            bool result = false;
            loadedData = default(T);
            //定义存档路径
            string fileName = StartPath + "/Settings/Config.json";
            try
            {
                loadedData = GetData<T>(fileName);
                result = true;
            }
            catch (Exception ex)
            {
                //MessageBox.Show(ex.Message);
                //result = false;
                throw ex;
            }

            return result;
        }
    }
}
