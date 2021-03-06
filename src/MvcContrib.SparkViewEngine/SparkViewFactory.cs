// Copyright 2008-2009 Louis DeJardin - http://whereslou.com
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// 
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MvcContrib.ViewFactories;
using Spark;

namespace MvcContrib.SparkViewEngine
{
    public class SparkViewFactory : Spark.Web.Mvc.SparkViewFactory
    {
        public SparkViewFactory()
        {

        }

        public SparkViewFactory(ISparkSettings settings)
            : base(settings)
        {

        }

        public IViewSourceLoader ViewSourceLoader
        {
            get
            {
                if (ViewFolder is ViewSourceLoaderWrapper)
                    return ((ViewSourceLoaderWrapper)ViewFolder).ViewSourceLoader;

                return null;
            }
            set
            {
                ViewFolder = new ViewSourceLoaderWrapper(value);
            }
        }
    }
}
