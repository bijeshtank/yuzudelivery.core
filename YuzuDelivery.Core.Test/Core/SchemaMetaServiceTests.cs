﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using Rhino.Mocks;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using YuzuDelivery.Core;

namespace YuzuDelivery.Core.Test
{
    public class SchemaMetaServiceTests
    {
        public ISchemaMetaPropertyService schemaMetaPropertyService;

        public SchemaMetaService svc;
        public IYuzuConfiguration config;

        public string jsonPaths;
        public string jsonOfTypeParent;
        public string jsonOfTypeChild;

        [TestFixtureSetUp]
        public void FixtureSetup()
        {
            YuzuConstants.Reset();
            YuzuConstants.Initialize(new YuzuConstantsConfig());
        }

        [SetUp]
        public void Setup()
        {
            schemaMetaPropertyService = MockRepository.GenerateStub<ISchemaMetaPropertyService>();
            config = MockRepository.GenerateStub<IYuzuConfiguration>();
            config.SchemaMetaLocations = new List<IDataLocation>();

            svc = MockRepository.GeneratePartialMock<SchemaMetaService>(new object[] { schemaMetaPropertyService, config });

            jsonPaths = @"{
                'refs': {
                    '/contentRows': [
                        '/parTest', '/parTest2'
                    ]
                }
            }";

            jsonOfTypeParent = @"{
                'refs': {
                    '/content': [
                        '/parDataGridRows^parGrid'
                    ],
                }
            }";

            jsonOfTypeChild = @"{
                'parGrid': {
                    'refs': {
                        '/rows/columns/items': [
                            '/parRte', '/parImage'
                        ]
                    }
                },
                'anyOfTypes': [
                    'parGrid'
                ]
            }";
        }

        [Test]
        public void Get_given_path_data_with_paths_at_property_then_convert_json_refs_to_vm_property_names()
        {
            var p = CreatePropertyInfo("ContentRows", "vmBlock_Test");
            var pathsJson = JObject.Parse(jsonPaths);

            schemaMetaPropertyService.Stub(x => x.Get(p)).Return((p.DeclaringType, "/contentRows"));
            svc.Stub(x => x.GetPathFileData(p.DeclaringType)).Return(pathsJson);

            var output = svc.Get(p, "refs");

            Assert.AreEqual("vmBlock_Test", output[0]);
            Assert.AreEqual("vmBlock_Test2", output[1]);
        }

        [Test]
        public void When_property_has_of_type_then_return_as_part_of_the_vm_name()
        {
            var p = CreatePropertyInfo("ContentGrid", "vmBlock_Test");
            var pathsJson = JObject.Parse(jsonOfTypeParent);

            svc.Stub(x => x.GetPathFileData(p.DeclaringType)).Return(pathsJson);

            var output = svc.Get(p.DeclaringType, "refs", "/content");

            Assert.AreEqual("vmBlock_DataGridRows^parGrid", output[0]);
        }

        [Test]
        public void Get_given_property_of_type_and_path_then_convert_json_refs_to_vm_property_names()
        {
            var p = CreatePropertyInfo("ContentGrid", "vmBlock_Test");
            var pathsJson = JObject.Parse(jsonOfTypeChild);

            svc.Stub(x => x.GetPathFileData(p.DeclaringType)).Return(pathsJson);

            var output = svc.Get(p.DeclaringType, "refs", "/rows/columns/items", "parGrid");

            Assert.AreEqual("vmBlock_Rte", output[0]);
            Assert.AreEqual("vmBlock_Image", output[1]);
        }

        [Test]
        public void Get_given_property_type_is_sub_then_use_root_component()
        {
            var p = CreatePropertyInfo("ContentGrid", null, "vmSub_TestItem");
            var blockType = CreateType("vmBlock_Test", new PropertyInfo[] { p });
            var pathsJson = JObject.Parse(jsonOfTypeChild);

            StubConfigViewmodels(blockType);
            svc.Stub(x => x.GetPathFileData(blockType)).Return(pathsJson);

            var output = svc.Get(p.PropertyType, "refs", "/rows/columns/items", "parGrid");

            Assert.AreEqual("vmBlock_Rte", output[0]);
            Assert.AreEqual("vmBlock_Image", output[1]);
        }

        [Test]
        public void GetPathFileData_given_vm_property_when_paths_file_exists_then_return_parsed_path_file_object()
        {
            var p = CreatePropertyInfo("ContentRows", "vmBlock_Test");
            CreatePathDataLocations(new string[] { "c:/test" });
            StubPathFile("c:/test", "vmBlock_Test", "path");

            var output = svc.GetPathFileData(p.DeclaringType);

            Assert.AreEqual(JObject.Parse(jsonPaths), output);
        }

        [Test]
        public void GetPathFileData_given_two_locations_when_paths_file_exists_in_second_location_then_return_parsed_path_file_object()
        {
            var p = CreatePropertyInfo("ContentRows", "vmBlock_Test");
            CreatePathDataLocations(new string[] { "c:/test", "c:/test2" });
            StubPathFile("c:/test", "vmBlock_Test", "not-here", false);
            StubPathFile("c:/test2", "vmBlock_Test", "path");

            var output = svc.GetPathFileData(p.DeclaringType);

            Assert.AreEqual(JObject.Parse(jsonPaths), output);
        }

        [Test, ExpectedException(ExpectedMessage = "Schema meta file not found for vmBlock_Test")]
        public void GetPathFileData_given_path_file_found_then_throw_exception()
        {
            var p = CreatePropertyInfo("ContentRows", "vmBlock_Test");

            var output = svc.GetPathFileData(p.DeclaringType);
        }

        [Test]
        public void GetPossiblePathFileName_return_possible_paths_with_par_prefix_and_without_prefix()
        {
            var output = svc.GetPossiblePathFileName("c:/test", "vmPage_Test");

            Assert.AreEqual("c:/test/Test.schema", output[0]);
            Assert.AreEqual("c:/test/parTest.schema", output[1]);
        }

        public void CreatePathDataLocations(string[] locations)
        {
            var schemaMetaLocations = locations.Select(x => new DataLocation() { Path = x }).Cast<IDataLocation>().ToList();

            config.SchemaMetaLocations = schemaMetaLocations;
        }

        public void StubPathFile(string rootPath, string declaringTypeName, string filePath, bool exists = true)
        {
            svc.Stub(x => x.GetPossiblePathFileName(rootPath, declaringTypeName)).Return(new string[] { filePath });
            svc.Stub(x => x.FileExists(filePath)).Return(exists);
            svc.Stub(x => x.FileRead(filePath)).Return(jsonPaths);
        }

        public PropertyInfo CreatePropertyInfo(string name, string declaringTypeName, string propertyTypeName = null)
        {
            var propertyInfo = MockRepository.GenerateMock<PropertyInfo>();
            propertyInfo.Stub(x => x.Name).Return(name);

            if (declaringTypeName != null)
            {
                var declaringType = CreateType(declaringTypeName);
                propertyInfo.Stub(x => x.DeclaringType).Return(declaringType);
            }

            if (propertyTypeName != null)
            {
                var propertyType = CreateType(propertyTypeName);
                propertyInfo.Stub(x => x.PropertyType).Return(propertyType);
            }

            return propertyInfo;
        }

        public Type CreateType(string name, PropertyInfo[] properties = null)
        {
            var type = MockRepository.GenerateStub<Type>();
            type.Stub(x => x.Name).Return(name);
            type.Stub(x => x.GetProperties()).Return(properties);
            return type;
        }

        public void StubConfigViewmodels(Type type)
        {
            var viewmodels = new List<Type>();
            viewmodels.Add(type);

            config.Stub(x => x.ViewModels).Return(viewmodels);
        }

    }
}
