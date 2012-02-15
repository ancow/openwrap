using System;
using System.Collections.Generic;
using System.Linq;
using OpenWrap.Collections;

namespace OpenWrap.PackageManagement.DependencyResolvers
{
    public class PackageResolverVisitor<T> where T : class
    {
        protected IEnumerable<T> Packages { get; private set; }
        protected Func<IEnumerable<T>, IEnumerable<T>> Strategy { get; private set; }

        public PackageResolverVisitor(
            IEnumerable<T> allPackages, 
            Func<T, IEnumerable<Func<T, bool?>>> dependencyReader, 
            Func<IEnumerable<T>, IEnumerable<T>> strategy, 
            IEnumerable<T> success = null, 
            IEnumerable<T> fail = null)
        {
            Strategy = strategy;
            Packages = allPackages;
            DependencyReader = dependencyReader;
            SuccessfulPackages = success.CopyOrNew();
            IncompatiblePackages = fail.CopyOrNew();
        }

        public Func<T, IEnumerable<Func<T, bool?>>> DependencyReader { get; set; }
        public ICollection<T> IncompatiblePackages { get; set; }
        public ICollection<T> SuccessfulPackages { get; set; }


        public virtual bool Visit(IEnumerable<Func<T, bool?>> dependencies)
        {
            return dependencies.All(info => VisitDependency(null, info));
        }

        protected virtual PackageResolverVisitor<T> CreateNestedResolver(IEnumerable<T> allPackages, 
                                                                         Func<T, IEnumerable<Func<T, bool?>>> dependencyReader, 
                                                                         Func<IEnumerable<T>, IEnumerable<T>> strategy, 
                                                                         ICollection<T> successfulPackages, 
                                                                         ICollection<T> incompatiblePackages)
        {
            return new PackageResolverVisitor<T>(allPackages, dependencyReader, strategy, successfulPackages, incompatiblePackages);
        }

        protected virtual bool VisitDependency(T package, Func<T, bool?> dependency)
        {
            var result = SuccessfulPackages.Select(dependency)
                .FirstOrDefault(_ => _ != null);
            
            if (result == true) return true;
            if (result == false)
            {
                var incompatible = SuccessfulPackages.First(_ => dependency(_) == false);
                IncompatiblePackages.Add(incompatible);
                SuccessfulPackages.Remove(incompatible);
                return false;
            }

            var matchingPackages =
                Strategy(Packages
                    .Except(IncompatiblePackages)
                    .Where(_ => dependency(_) == true))
                    .ToList();

            var b = VisitPackages(matchingPackages);
            return b;
        }


        protected virtual bool VisitPackage(T package)
        {
            var newResolver = CreateNestedResolver(Packages, DependencyReader, Strategy, SuccessfulPackages, IncompatiblePackages);
            newResolver.SuccessfulPackages.Add(package);
            bool success;
            if (newResolver.Visit(DependencyReader(package)))
            {
                NestedPackageSucceeds(newResolver);
                success = true;
            }
            else
            {
                NestedPackageFail(newResolver);
                success = false;
            }

            return success;
        }

        protected virtual void NestedPackageFail(PackageResolverVisitor<T> newResolver)
        {
            IncompatiblePackages.AddRange(newResolver.IncompatiblePackages);
        }

        protected virtual void NestedPackageSucceeds(PackageResolverVisitor<T> newResolver)
        {
            SuccessfulPackages = newResolver.SuccessfulPackages;
            IncompatiblePackages = newResolver.IncompatiblePackages;
        }

        protected virtual bool VisitPackages(IEnumerable<T> matchingPackages)
        {
            return matchingPackages.Any(VisitPackage);
        }
    }
}